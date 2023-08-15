﻿#if UNITY_EDITOR
// ---------------------------------------------------------------------------- 
// Author: Ryan Hipple
// Date:   05/01/2018
// Source: https://github.com/roboryantron/UnityEditorJunkie
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MyBox.Internal
{
	/// <summary>
	/// A popup window that displays a list of options and may use a search
	/// string to filter the displayed content
	/// </summary>
	public class SearchablePopup : PopupWindowContent
	{
		#region -- Constants --------------------------------------------------

		/// <summary> Height of each element in the popup list. </summary>
		private const float ROW_HEIGHT = 16.0f;

		/// <summary> Name to use for the text field for search. </summary>
		private const string SEARCH_CONTROL_NAME = "EnumSearchText";

		#endregion -- Constants -----------------------------------------------

		#region -- Static Functions -------------------------------------------

		/// <summary> Show a new SearchablePopup. </summary>
		/// <param name="activatorRect">
		/// Rectangle of the button that triggered the popup.
		/// </param>
		/// <param name="options">List of strings to choose from.</param>
		/// <param name="current">
		/// Index of the currently selected string.
		/// </param>
		/// <param name="onSelectionMade">
		/// Callback to trigger when a choice is made.
		/// </param>
		public static void Show(Rect activatorRect, string[] options, int current, Action<int> onSelectionMade)
		{
			SearchablePopup win =
				new SearchablePopup(options, current, onSelectionMade);
			PopupWindow.Show(activatorRect, win);
		}

		/// <summary>
		/// Force the focused window to redraw. This can be used to make the
		/// popup more responsive to mouse movement.
		/// </summary>
		private static void Repaint()
		{
			EditorWindow.focusedWindow.Repaint();
		}

		/// <summary> Draw a generic box. </summary>
		/// <param name="rect">Where to draw.</param>
		/// <param name="tint">Color to tint the box.</param>
		private static void DrawBox(Rect rect, Color tint)
		{
			Color c = GUI.color;
			GUI.color = tint;
			GUI.Box(rect, "", Selection);
			GUI.color = c;
		}

		#endregion -- Static Functions ----------------------------------------

		#region -- Helper Classes ---------------------------------------------

		/// <summary>
		/// Stores a list of strings and can return a subset of that list that
		/// matches a given filter string.
		/// </summary>
		private class FilteredList
		{
			/// <summary>
			/// An entry in the filtered list, mapping the text to the
			/// original index.
			/// </summary>
			public struct Entry
			{
				public int index;
				public string text;
			}

			/// <summary> All possible items in the list. </summary>
			private readonly string[] allItems;

			/// <summary> Create a new filtered list. </summary>
			/// <param name="items">All The items to filter.</param>
			public FilteredList(string[] items)
			{
				allItems = items;
				Entries = new List<Entry>();
				UpdateFilter("");
			}

			/// <summary> The current string filtering the list. </summary>
			public string Filter { get; private set; }

			/// <summary> All valid entries for the current filter. </summary>
			public List<Entry> Entries { get; private set; }

			/// <summary> Total possible entries in the list. </summary>
			public int MaxLength
			{
				get { return allItems.Length; }
			}
			
			/// <summary>
			/// The size and position of the first row in the scrollable.
			/// Used to calculate the minimum scrollable window window width using the widest row's text width.
			/// </summary>
			public Rect RowRect;

			/// <summary>
			/// Used to calculate the minimum scrollable window window width using the widest row's text width.
			/// </summary>
			public float WindowWidth = 0.0f;

			/// <summary>
			/// Sets a new filter string and updates the Entries that match the
			/// new filter if it has changed.
			/// </summary>
			/// <param name="filter">String to use to filter the list.</param>
			/// <returns>
			/// True if the filter is updated, false if newFilter is the same
			/// as the current Filter and no update is necessary.
			/// </returns>
			public bool UpdateFilter(string filter)
			{
				if (Filter == filter)
					return false;

				Filter = filter;
				Entries.Clear();
				var rowWidth = 0.0f;

				GUIStyle styleScrollbar = GUI.skin.verticalScrollbar;
				GUIStyle styleBox = GUI.skin.box;
				GUIStyle styleLabel = GUI.skin.label;

				for (int i = 0; i < allItems.Length; i++)
				{
					if (string.IsNullOrEmpty(Filter) || allItems[i].ToLower().Contains(Filter.ToLower()))
					{
						Entry entry = new Entry
						{
							index = i,
							text = allItems[i]
						};
						if (string.Equals(allItems[i], Filter, StringComparison.CurrentCultureIgnoreCase))
							Entries.Insert(0, entry);
						else
							Entries.Add(entry);

						GUIContent content = new GUIContent(entry.text);
						var textSize = styleBox.CalcSize(content);
						rowWidth = Math.Max(rowWidth, textSize.x);
					}
				}
				
				RowRect = new Rect(styleBox.margin.left, 0, rowWidth + styleLabel.margin.horizontal, ROW_HEIGHT);
				WindowWidth = RowRect.width + styleBox.margin.horizontal + styleScrollbar.fixedWidth;

				return true;
			}
		}

		#endregion -- Helper Classes ------------------------------------------

		#region -- Private Variables ------------------------------------------

		/// <summary> Callback to trigger when an item is selected. </summary>
		private readonly Action<int> onSelectionMade;

		/// <summary>
		/// Index of the item that was selected when the list was opened.
		/// </summary>
		private readonly int currentIndex;

		/// <summary>
		/// Container for all available options that does the actual string
		/// filtering of the content.
		/// </summary>
		private readonly FilteredList list;

		/// <summary> Scroll offset for the vertical scroll area. </summary>
		private Vector2 scroll;

		/// <summary>
		/// Index of the item under the mouse or selected with the keyboard.
		/// </summary>
		private int hoverIndex;

		/// <summary>
		/// An item index to scroll to on the next draw.
		/// </summary>
		private int scrollToIndex;

		/// <summary>
		/// An offset to apply after scrolling to scrollToIndex. This can be
		/// used to control if the selection appears at the top, bottom, or
		/// center of the popup.
		/// </summary>
		private float scrollOffset;

		#endregion -- Private Variables ---------------------------------------

		#region -- GUI Styles -------------------------------------------------

		// GUIStyles implicitly cast from a string. This triggers a lookup into
		// the current skin which will be the editor skin and lets us get some
		// built-in styles.

#if UNITY_2021_3_OR_NEWER
		// Looks like these typos were fixed in Unity 2021.3.
		// These reference the GUIStyles that exist here:
		// https://github.com/Unity-Technologies/UnityCsReference/blob/2021.3/Editor/Mono/GUI/EditorStyles.cs
		private static readonly GUIStyle SearchBox = "ToolbarSearchTextField";
		private static readonly GUIStyle CancelButton = "ToolbarSearchCancelButton";
		private static readonly GUIStyle DisabledCancelButton = "ToolbarSearchCancelButtonEmpty";
		private static readonly GUIStyle Selection = "SelectionRect";
#else
		// Yeah, "Seach" instead of "Search", it's Unity's typo
		private static readonly GUIStyle SearchBox = "ToolbarSeachTextField";
		private static readonly GUIStyle CancelButton = "ToolbarSeachCancelButton";
		private static readonly GUIStyle DisabledCancelButton = "ToolbarSeachCancelButtonEmpty";
		private static readonly GUIStyle Selection = "SelectionRect";
#endif

		#endregion -- GUI Styles ----------------------------------------------

		#region -- Initialization ---------------------------------------------

		private SearchablePopup(string[] names, int currentIndex, Action<int> onSelectionMade)
		{
			list = new FilteredList(names);
			this.currentIndex = currentIndex;
			this.onSelectionMade = onSelectionMade;

			hoverIndex = currentIndex;
			scrollToIndex = currentIndex;
			scrollOffset = GetWindowSize().y - ROW_HEIGHT * 2;
		}

		#endregion -- Initialization ------------------------------------------

		#region -- PopupWindowContent Overrides -------------------------------

		public override void OnOpen()
		{
			base.OnOpen();
			// Force a repaint every frame to be responsive to mouse hover.
			EditorApplication.update += Repaint;
		}

		public override void OnClose()
		{
			base.OnClose();
			EditorApplication.update -= Repaint;
		}

		public override Vector2 GetWindowSize()
		{
			return new Vector2(
				Math.Max(base.GetWindowSize().x, list.WindowWidth),
				Mathf.Min(600, list.MaxLength * ROW_HEIGHT + EditorStyles.toolbar.fixedHeight)
			);
		}

		public override void OnGUI(Rect rect)
		{
			Rect searchRect = new Rect(0, 0, rect.width, EditorStyles.toolbar.fixedHeight);
			Rect scrollRect = Rect.MinMaxRect(0, searchRect.yMax, rect.width, rect.yMax);

			HandleKeyboard();
			DrawSearch(searchRect);
			DrawSelectionArea(scrollRect);
		}

		#endregion -- PopupWindowContent Overrides ----------------------------

		#region -- GUI --------------------------------------------------------

		private void DrawSearch(Rect rect)
		{
			if (Event.current.type == EventType.Repaint)
				EditorStyles.toolbar.Draw(rect, false, false, false, false);

			Rect searchRect = new Rect(rect);
			searchRect.xMin += 6;
			searchRect.xMax -= 6;
			searchRect.y += 2;
			searchRect.width -= CancelButton.fixedWidth;

			GUI.FocusControl(SEARCH_CONTROL_NAME);
			GUI.SetNextControlName(SEARCH_CONTROL_NAME);
			string newText = GUI.TextField(searchRect, list.Filter, SearchBox);

			if (list.UpdateFilter(newText))
			{
				hoverIndex = 0;
				scroll = Vector2.zero;
			}

			searchRect.x = searchRect.xMax;
			searchRect.width = CancelButton.fixedWidth;

			if (string.IsNullOrEmpty(list.Filter))
				GUI.Box(searchRect, GUIContent.none, DisabledCancelButton);
			else if (GUI.Button(searchRect, "x", CancelButton))
			{
				list.UpdateFilter("");
				scroll = Vector2.zero;
			}
		}

		private void DrawSelectionArea(Rect scrollRect)
		{
			Rect contentRect = new Rect(0, 0,
				scrollRect.width - GUI.skin.verticalScrollbar.fixedWidth,
				list.Entries.Count * ROW_HEIGHT);

			scroll = GUI.BeginScrollView(scrollRect, scroll, contentRect);

			var rowRect = list.RowRect;

			for (int i = 0; i < list.Entries.Count; i++)
			{
				if (scrollToIndex == i &&
				    (Event.current.type == EventType.Repaint
				     || Event.current.type == EventType.Layout))
				{
					Rect r = new Rect(rowRect);
					r.y += scrollOffset;
					GUI.ScrollTo(r);
					scrollToIndex = -1;
					scroll.x = 0;
				}

				if (rowRect.Contains(Event.current.mousePosition))
				{
					if (Event.current.type == EventType.MouseMove ||
					    Event.current.type == EventType.ScrollWheel)
						hoverIndex = i;
					if (Event.current.type == EventType.MouseDown)
					{
						onSelectionMade(list.Entries[i].index);
						EditorWindow.focusedWindow.Close();
					}
				}

				DrawRow(rowRect, i);

				rowRect.y = rowRect.yMax;
			}

			GUI.EndScrollView();
		}

		private void DrawRow(Rect rowRect, int i)
		{
			if (list.Entries[i].index == currentIndex)
				DrawBox(rowRect, Color.cyan);
			else if (i == hoverIndex)
				DrawBox(rowRect, Color.white);
								
			GUIStyle styleLabel = GUI.skin.label;
			Rect labelRect = new Rect(
				rowRect.position.x + styleLabel.margin.left,
				rowRect.position.y,
				rowRect.width - styleLabel.margin.horizontal,
				rowRect.height
			);

			GUI.Label(labelRect, list.Entries[i].text);
		}

		/// <summary>
		/// Process keyboard input to navigate the choices or make a selection.
		/// </summary>
		private void HandleKeyboard()
		{
			if (Event.current.type == EventType.KeyDown)
			{
				if (Event.current.keyCode == KeyCode.DownArrow)
				{
					hoverIndex = Mathf.Min(list.Entries.Count - 1, hoverIndex + 1);
					Event.current.Use();
					scrollToIndex = hoverIndex;
					scrollOffset = ROW_HEIGHT;
				}

				if (Event.current.keyCode == KeyCode.UpArrow)
				{
					hoverIndex = Mathf.Max(0, hoverIndex - 1);
					Event.current.Use();
					scrollToIndex = hoverIndex;
					scrollOffset = -ROW_HEIGHT;
				}

				if (Event.current.keyCode == KeyCode.Return)
				{
					if (hoverIndex >= 0 && hoverIndex < list.Entries.Count)
					{
						onSelectionMade(list.Entries[hoverIndex].index);
						EditorWindow.focusedWindow.Close();
					}
				}

				if (Event.current.keyCode == KeyCode.Escape)
				{
					EditorWindow.focusedWindow.Close();
				}
			}
		}

		#endregion -- GUI -----------------------------------------------------
	}
}
#endif
