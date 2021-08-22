/*
 * This file shows examples on how to:
 * 
 *		1) Edit the right-click context menus for ConsoleE
 *		2) Intercept or override the event when opening files or selecting items
 *		3) Format display string for extra info such as Time.time
 *		4) Get detailed information about log entries
 *	
 *		ConsoleE_Extensions.cs should be placed inside a folder with the name "Editor".
 * 
 *		OnFormatTime and OnFormatFrame are availabing in ConsoleE free.
 *		The remaining hooks require a pro license of ConsoleE.
 */

#if true // make true to enable the sample extensions in this file

#if !UNITY_EDITOR
#error ConsoleE_Extensions.cs is being compiled in non-editor build // ConsoleE is an editor-only extension
#endif

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ConsoleE
{
	/// <summary>
	/// This sample class is provided to show you how to install hooks to extend some aspects of ConsoleE
	/// </summary>
	[InitializeOnLoad]
	public class ConsoleE_Extensions
	{
		static ConsoleE_Extensions()
		{
			// every time you press Run, hooks need to be resync-ed with ConsoleE.
			// with [InitializeOnLoad], this register step will occur whenever you press play
			// only += is needed, no need for -= later
			// most events require ConsoleE Pro
			// note that Unity calls [InitializeOnLoad] very early during initialization. The ConsoleE window is not yet initialized. Many ConsoleE.Api functions will not work until a little later.
			// ConsoleE.onConsoleSettingsInitialized is called a little later during initialization.
			ConsoleE.Api.onClick += OnClick;   // event is triggered when user tries to open a file
			ConsoleE.Api.onBuildMenu += OnBuildMenu; // event is triggered when user opens a menu
			ConsoleE.Api.onSelection += OnSelection; // single item in the main area has been selected, or selection in the callstack has changes
			ConsoleE.Api.onFormatObject += FormatObjectText;// for formatting Object.name
			ConsoleE.Api.onFormatFilename += FormatFilenameText;// for formatting script filename
			ConsoleE.Api.onFormatCallstackEntry += FormatCallstackEntry;// for formatting primary callstack row

			// these hooks are available in ConsoleE free:
			ConsoleE.Api.onFormatTime += FormatTime; // for formatting Time.time
			ConsoleE.Api.onFormatFrame += FormatFrameCount; // for formatting Time.frameCount

			ConsoleE.Api.onConsoleInitialized += OnConsoleInitialized; // called after ConsoleE has been initialized, when it's fully safe to interact with ConsoleE's public C# API
		}

		/// <summary>
		/// Called after ConsoleE has been initialized.
		/// It's possible this is called twice like if the user opens and closes the console window multiple times.
		/// Now it's safe to interact with the ConsoleE_Tab api
		/// </summary>
		static void OnConsoleInitialized(OnConsoleInitializedParams p)
		{
			// tab.onCheckForMatch is called for a tab category when ConsoleE is trying to determine if a log entry should be included.
			// tab.onCheckForMatch is called for tabs based on their sort order in the tab settings dialog, the tabs at the bottom of the list are checked first (The tab 'Error' is usually last).
			// If tab matches with a log entry, it usually absorbs or handles the log entry.  When this happens, lower priority tabs are not even considered (onCheckForMatch will not be called for them).
			// There is a tab setting 'Consume Matches' (in advanced settings/tabs/misc) if you want a log entry to be included in multiple tabs at once.

			ConsoleTab tab = null;

			// Cannot override matching behavior of builtin tabs, tab.onCheckForMatch is only for custom tabs (or compiler tabs)
			// tab = ConsoleE.Api.FindConsoleTabByName("Info"); 

			tab = ConsoleE.Api.FindConsoleTabByName("Error, Compiler");
			if(tab != null)
			{
				tab.onCheckForMatch = CheckForMatch_ErrorCompiler;
			}

			tab = ConsoleE.Api.FindConsoleTabByName("Warning, Compiler");
			if(tab != null)
			{
				tab.onCheckForMatch = CheckForMatch_WarningCompiler; // handle rare case of opening and closing ConsoleE window multiple times
			}
		}

		private const string pattern = "	\\[string \"(.*)\"\\]:(\\d+): .*";
		private static Regex _regex = new Regex(pattern);

		/// <summary>
		/// Called whenever a file is about to be opened by the console.  In this method, you have the option of overriding the open file behavior.
		/// Requires ConsoleE PRO.
		/// </summary>
		/// <param name="p">If p.OpenUsingDefaultMethod is true, ConsoleE will open the file normally.  If false, ConsoleE will no nothing.
		/// p.windowArea describes if the click ocurred in the main area or in the callstack.
		/// p.logEntry contains info about the selected log entry. If multi entries are selected, p.logEntry is null.
		/// p.logEntry.tabCategory is the tab category for the log item clicked.
		/// p.logEntry.rows are the rows of the log entry. Rows[0] is the Log msg, the rest is the callstack.
		/// p.filename is filename that should be open (considering wrapper list).
		/// p.externalEditorPath if user used "Open with", this indicates which editor should be used to open the file.
		/// p.indexOpenWithOption if user used "Open with, this is the index of the menu item in "Open with" that was clicked.
		/// p.indexRowSelected, which callstack row was clicked, or -1 if none.
		/// </param>
		/// <returns></returns>
		static public void OnClick(OnClickParams p)
		{
 			if(p.filename == null) // if failed to parse filename
            {
	            var match = _regex.Match(p.logEntry.rows[p.indexRowSelected]);
	            if (match.Success)
	            {
		            string filename = match.Groups[1].Captures[0].Value;
		            string lineNumberStr = match.Groups[2].Captures[0].Value;
		            // string relativePath = string.Format("Assets/Lua/{0}.lua", filename);
		            string luaPattern = ConsoleE_Extensions_Option.HasOption()
			            ? ConsoleE_Extensions_Option.Instance.LuaPathPattern
			            : "Assets/Lua/{0}.lua";
		            string relativePath = string.Format(luaPattern, filename);
		            int lineNumber = Convert.ToInt32(lineNumberStr);

		            var guid = AssetDatabase.AssetPathToGUID(relativePath);
		            if(string.IsNullOrEmpty(guid))
		            {
			            p.openUsingDefaultMethod = true; // true if not handled in which case ConsoleE will open the file using the default code, set to false if you do not want ConsoleE to open the file
		            }
		            else
		            {
			            OpenEditor(p, relativePath, lineNumber);
		            }
	            }
				p.openUsingDefaultMethod = true;
				return;
			}

			string selectedEntryText = string.Empty;
			if(p.logEntry != null && p.logEntry.rows.Length > 0)
			{
				selectedEntryText = p.logEntry.rows[0];
				if(selectedEntryText.Length > 40)
					selectedEntryText = selectedEntryText.Substring(0, 35) + "...";
			}
			UnityEngine.Debug.Log(string.Format("OnClick: {0}\nLine: {1}, Editor:{2}, Callstack index selected:{3}, Area:{4}\nSelected entry text: {5}", p.filename, p.lineNumber, p.externalEditorPath, p.indexRowSelected, p.windowArea, selectedEntryText));
			p.openUsingDefaultMethod = true; // true if not handled in which case ConsoleE will open the file using the default code, set to false if you do not want ConsoleE to open the file

			/* uncomment to open file in Script Inspector 3
			
			if(p.indexOpenWithOption != -1 || // user is using right-click "Open With"
				p.filename == null) // if failed to parse filename
			{
				p.openUsingDefaultMethod = true;
				return;
			}			
			
			string relativePath = FilenameRelativeToAssets(p.filename);

			var guid = AssetDatabase.AssetPathToGUID(relativePath);

			if(string.IsNullOrEmpty(guid))
			{
				p.openUsingDefaultMethod = true; // true if not handled in which case ConsoleE will open the file using the default code, set to false if you do not want ConsoleE to open the file
			}
			else
			{
				// open in Script Inspector 3
				p.openUsingDefaultMethod = false;
				FGCodeWindow.OpenAssetInTab(guid, p.lineNumber);
			}
			return;
			*/
		}

		private static void OpenEditor(OnClickParams p, string relativePath, int lineNumber)
		{
			var openEditorMethod = ConsoleE_Extensions_Option.OpenEditor.UnityInternal;
			string openEditorPath = string.Empty;
			string openEditorArgumentPattern = string.Empty;

			if (ConsoleE_Extensions_Option.HasOption())
			{
				openEditorMethod = ConsoleE_Extensions_Option.Instance.CurrentOpenEditor;
				openEditorPath = ConsoleE_Extensions_Option.Instance.CurrentOpenEditorPath;
				openEditorArgumentPattern = ConsoleE_Extensions_Option.Instance.ArgumentPattern;
			}

			switch (openEditorMethod)
			{
				case ConsoleE_Extensions_Option.OpenEditor.UnityInternal:
					p.openUsingDefaultMethod = false;
					UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(relativePath, lineNumber);
					break;
				case ConsoleE_Extensions_Option.OpenEditor.AssociatedByExtension:
					p.openUsingDefaultMethod = false;
					var obj = AssetDatabase.LoadMainAssetAtPath(relativePath);
					AssetDatabase.OpenAsset(obj, lineNumber);
					break;
				case ConsoleE_Extensions_Option.OpenEditor.CustomEditorPath:
					string absPath = Path.GetFullPath(relativePath);
					System.Diagnostics.Process.Start(openEditorPath, String.Format(openEditorArgumentPattern, absPath, lineNumber));
					break;
				default:
					p.openUsingDefaultMethod = true;
					break;
			}
		}

		/// <summary>
		/// Override the matching behaviour of a tab.
		/// Try write efficient code here, it's called every time a new log entry is added.
		/// Note that if no tab is matched, as a fallback mechanism ConsoleE will place the item in one of the built-in tabs (Log, Warning, Error) depending on the LogType of entry.
		/// It's also called when the tab is refreshed: if you change the tab settings -OR- user is typing text into text search field
		/// </summary>
		/// <param name="p"></param>
		private static bool CheckForMatch_ErrorCompiler(OnCheckForMatchParams p)
		{
			if(p.logEntryText.Contains("The variable"))
			{
				return true;
			}
			return p.CheckForMatchDefault(); // do default matching behavior 
		}

		/// <summary>
		/// Override the matching behaviour of a tab.
		/// Try write efficient code here, it's called every time a new log entry is added.
		/// Note that if no tab is matched, as a fallback mechanism ConsoleE will place the item in one of the built-in tabs (Log, Warning, Error) depending on the LogType of entry.
		/// It's also called when the tab is refreshed: if you change the tab settings -OR- user is typing text into text search field
		/// </summary>
		/// <param name="p"></param>
		private static bool CheckForMatch_WarningCompiler(OnCheckForMatchParams p)
		{
			// p.isMatch will be true or false depending on the default tab behavior
			// if we change the value of p.isMatch, we alter the matching behavior of the tab
			if(p.logEntryText.Contains("The variable"))
			{
				// note that if no tab is matched, as a fallback mechanism ConsoleE will place the item in one of the built-in tabs (Log, Warning, Error) depending on the LogType of entry.
				return false;
			}
			else
			{
				if(p.logEntryText.Contains("Forced to compilter warning tab"))
				{
					return true; // override value, if true, we are forcing this item to be in this tab
				}
			}
			return p.CheckForMatchDefault(); // do default matching behavior
		}

		/// <summary>
		/// This is called when a single item is selected, or if a single item is deselected in the main area. It is also called if selection occurs in the callstack area.
		/// </summary>
		/// <param name="p">
		/// p.windowArea describes if the selection ocurred in the main area or in the callstack.
		/// p.logEntry contains info about the selected log entry. If multi entries are selected, p.logEntry is null.
		/// p.logEntry.tabCategory is the tab category for the log item clicked.
		/// p.logEntry.rows are the rows of the log entry. Rows[0] is the Log msg, the rest is the callstack.
		/// </param>
		private static void OnSelection(OnSelectionParams p)
		{
			var e = p.logEntry;
			if(e != null)
			{
				if(p.windowArea == WindowAreas.mainArea)
				{
					e.SelectObjectInUnity();
				}
				else // callstack
				{
					int i = e.indexRowSelected;
					if(i >= 0 && i < e.rows.Length)
					{
						int lineNumber;
						string filename = LogEntry.ParseForFilename(out lineNumber, e.rows[i], true, true);
						if(filename != null)
						{
							UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(filename, typeof(UnityEngine.Object));
							if(obj != null)
							{
								Selection.activeObject = obj;
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// This is called whenever a context menu is about to be shown. In this method, you have the option of editing the menu before it is shown.
		/// p.Menu is of type GenericMenuEx which is like Unity's GenericMenu but provides extended functionality.
		/// Requires ConsoleE PRO.
		/// </summary>
		/// <param name="p">p.menu is the list of menu items. 
		/// p.windowArea describes what type of menu we are building.  
		/// p.logEntry contains info about the selected log entry. If multi entries are selected, p.logEntry is null.
		/// </param>
		/// <returns></returns>
		static public void OnBuildMenu(OnBuildMenuParams p)
		{
			p.menu.AddItem(new GUIContent("Clear All [HOOK]"), false, Api.ClearLog);
			p.menu.AddItem(new GUIContent("Clear Compiler Warnings Only [HOOK]"), false, ClearCompilerWarningsOnly);
			p.menu.AddItem(new GUIContent("Select Last Error [HOOK]"), false, SelectLastError);
			p.menu.AddItem(new GUIContent("Show Only Errors [HOOK]"), false, ShowOnlyErrors);

			if(p.windowArea == WindowAreas.toolbar)
				p.menu.AddItem(new GUIContent("Toolbar menu option [HOOK]"), false, OnOptionMenuClicked, p);

			p.menu.AddItem(new GUIContent("Insert at index 1 [HOOK]"), false, OnOptionMenuInserted, p, 1);

			if(p.windowArea == WindowAreas.callstackArea)
			{
				// example of how you can edit the existing default menu list
				var entries = p.menu.Entries;
				for(int i = 0; i < entries.Count; i++)
				{
					if(entries[i].Text.StartsWith("Search Web"))
					{
						p.menu.RemoveAt(i);
						break;
					}
				}
			}

			p.menu.AddSeparator(string.Empty);

			if(p.logEntry != null && p.logEntry.rows.Length > 0)
			{
				// example of using info from the selected entry
				string text = p.logEntry.rows[0];
				if(text.Length > 20)
					text = text.Substring(0, 15) + "...";
				p.menu.AddItem(new GUIContent("Selected Entry Info [HOOK]"), false, OnOptionMenuClicked, p);
			}
			else
			{
				p.menu.AddDisabledItem(new GUIContent("Selected Entry Info [HOOK]"));
			}

		   	if(p.windowArea == WindowAreas.mainArea)
			{
				if(Api.selectedEntrySingle_AsUnityIndex != -1) // if there is a singlely-selected entry
				{
					int indexInsert = p.menu.Entries.FindIndex(f => f.Text == "Remove"); // find index where to insert menu item
					p.menu.AddItem(new GUIContent("Remove Before Selection [HOOK]"), false, ClearEntriesBeforeSelection, indexInsert);
				}
			}
			
			p.showWindow = true; // set this to true to tell ConsoleE to show the menu.  This is ignored if Unity is building the menu for IHasCustomMenu.
		}

		/// <summary>
		/// This will uncheck all category tab buttons except the Error tab.
		/// </summary>
		static void ShowOnlyErrors()
		{
			foreach(ConsoleTab tab in Api.consoleTabs) // requires ConsoleE Pro
			{
				tab.isToggled = tab.name == "Error"; // no need to worry about the compiler category because the categores are linked: // || tab.name == "Error, Compiler")
			}
		}

		/// <summary>
		/// This removes all log entries that are matching with the tab "Warning, Compiler".
		/// </summary>
		static void ClearCompilerWarningsOnly()
		{
			ConsoleTab tab = Api.FindConsoleTabByName("Warning, Compiler");	 // requires ConsoleE Pro
			if (tab != null)
			{
				foreach(var indexUnity in tab.GetEntriesInReverseOrder())  // requires ConsoleE Pro
				{
					Api.RemoveEntryByUnityIndex(indexUnity);  // requires ConsoleE Pro
				}
			}
		}

		/// <summary>
		/// Finds the last message of a particular tab (Error) and selects that log entry.
		/// </summary>
		static void SelectLastError()
		{
			ConsoleTab tab = Api.FindConsoleTabByName("Error");	 // requires ConsoleE Pro
			if (tab != null)
			{
				var list = tab.GetEntriesInReverseOrder();	// requires ConsoleE Pro
				if(list.Count > 0)
					Api.SelectEntryByUnityIndex(list[0]);  // requires ConsoleE Pro
			}
		}

		/// <summary>
		/// This function removes all entries above the selected item.
		/// This function shows off the concept of "Render Index" which is the index in the list of visible entries.
		/// The first visible log entry always has a render index of zero.  The last is always Api.countEntriesVisible-1.
		/// "Unity Index" is an internal index assigned by the Unity Editor, it applies to entries that are not visible. 
		/// Log entries are not visible if their tab is unchecked or the search filter is active.
		/// </summary>
		static void ClearEntriesBeforeSelection()
		{
			LogEntry logEntry = Api.selectedEntrySingle;
			if(logEntry != null)
			{				 
				int indexRendered = logEntry.indexRender;

				// start with the bottom of the list so that render indexes don't change as we remove entries
				for(int i = indexRendered-1; i >= 0; i--)
				{
					int indexUnity = Api.UnityIndexFromRenderIndex(i);
					if(indexUnity != -1) // shouldn't ever be -1..but check just in case internal data is not consistent
						Api.RemoveEntryByUnityIndex(indexUnity);
				}
			}
		}

		private static void OnOptionMenuInserted(object userData)
		{
			var p = (OnBuildMenuParams) userData;
			UnityEngine.Debug.Log("Menu option clicked: inserted entry from " + p.windowArea.ToString());
		}

		static void OnOptionMenuClicked(object userData)
		{
			var p = (OnBuildMenuParams) userData;

			UnityEngine.Debug.Log(string.Format("Menu option clicked: selected entry (tab=\"{0}\", render index={1}, unity index={2})", p.logEntry.tabCategory, p.logEntry.indexRender, p.logEntry.indexUnity));

			string s = p.logEntry.GetCallstackPartialTextSelection();
			if(s != null)
			{
				UnityEngine.Debug.Log("Partially selected text in callstack:\n[START]\n" + s + "\n[END]\n");
			}
		}

		/// <summary>
		/// For overriding how the time column is displayed.  ConsoleE optionally can display Time.time for when Debug.Log() is called.  This hook allows you to format how time is displayed.
		/// For ConsoleE FREE & PRO.
		/// </summary>
		/// <param name="p">p.area is the area in console where this item is being rendered</param>
		/// <param name="time">value of Time.time when Debug.Log() was called, -1 if app not running</param>
		/// <param name="isTimeApproximate">true if the console was not able to determine exactly when the log message was created.  This can happen when a log entry is added before the ConsoleE window is enabled or if a Debug.Log() occurs during Application.logMessageReceived.</param>
		/// <returns>the time string, do not return null</returns>
		static string FormatTime(OnFormatItemParams p, float time, bool isTimeApproximate)
		{
			if(time >= 0)
			{
				string s = !isTimeApproximate ? time.ToString("0.000") : time.ToString("~0.000");
				if(p.area != ExtraItemArea.callstack)
					return s;
				return "Time: " + s;
			}
			return string.Empty; // log while app not running
		}

		/// <summary>
		/// For overriding how the frame count column is displayed.  ConsoleE optionally can display Time.frameCount for when Debug.Log() is called.  This hook allows you to format how frame count value is displayed.
		/// For ConsoleE FREE & PRO.
		/// </summary>
		/// <param name="p">p.area is the area in console where this item is being rendered</param>
		/// <param name="frameCount">value of Time.frameCount when Debug.Log() was called, -1 if app is not running</param>
		/// <param name="isFrameCountApproximate">true if the console was not able to determine exactly when the log message was created.  This can happen when a log entry is added before the ConsoleE window is enabled or if a Debug.Log() occurs during Application.logMessageReceived.</param>
		/// <returns>the frame count string, do not return null</returns>
		static string FormatFrameCount(OnFormatItemParams p, int frameCount, bool isFrameCountApproximate)
		{
			if(frameCount >= 0)
			{
				string s = !isFrameCountApproximate ? frameCount.ToString() : frameCount.ToString("~0");
				if(p.area != ExtraItemArea.callstack)
					return s;
				return "Frame: " + s;
			}
			return string.Empty; // log while app not running
		}

		/// <summary>
		/// For overriding how the "object" column is displayed.  ConsoleE optionally can display the name of the object passed to Debug.Log().  This hook allows you to format how the string is displayed in the console.
		/// For ConsoleE PRO.
		/// </summary>
		/// <param name="p">p.area is the area in console where this item is being rendered</param>
		/// <param name="objectName">Name of the object being displayed</param>
		/// <returns>object text display string, do not return null</returns>
		static string FormatObjectText(OnFormatItemParams p, string objectName)
		{
			if(p.area != ExtraItemArea.callstack)
				return objectName;
			return "Object: " + objectName;
		}

		/// <summary>
		/// For overriding how the "filename" column is displayed.  ConsoleE optionally can display the filename associated with the Debug.Log() call.  This hook allows you to format how the string is displayed in the console.
		/// For ConsoleE PRO.
		/// </summary>
		/// <param name="p">p.area is the area in console where this item is being rendered</param>
		/// <param name="filename">Full filename of the script file</param>
		/// <param name="hideExtension">user has clicked checkbox to hide extension</param>
		/// <returns>filename string to display, do not return null</returns>
		static string FormatFilenameText(OnFormatItemParams p, string filename, bool hideExtension)
		{
			string s = hideExtension ? System.IO.Path.GetFileNameWithoutExtension(filename) : System.IO.Path.GetFileName(filename);
			if(p.area != ExtraItemArea.callstack)
				return s;
			return "Filename: " + s;
		}

		/// <summary>
		/// For overriding how the "callstack entry" column is displayed.  The callstack entry is the primary row in the callstack, skipping past any wrapper functions.
		/// For ConsoleE PRO.
		/// </summary>
		/// <param name="p">p.area is the area in console where this item is being rendered</param>
		/// <param name="callstackEntry">text for the row in the callstack that ConsoleE has deemed primary</param>
		/// <param name="indexRow">index of the row in the callstack that ConsoleE has deemed primary, -1 if ConsoleE does not find a callstack row</param>
		/// <returns>filename string to display, do not return null</returns>
		static string FormatCallstackEntry(OnFormatItemParams p, string callstackEntry, int indexRow)
		{
			if(indexRow == -1) // if no primary callstack row
				return string.Empty;
			if(indexRow >= 0 && indexRow < p.logEntry.rows.Length)
				return p.logEntry.rows[indexRow];
			return callstackEntry;
		}
	}
}
#endif
