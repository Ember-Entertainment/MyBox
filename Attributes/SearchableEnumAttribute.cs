// ---------------------------------------------------------------------------- 
// Author: Ryan Hipple
// Date:   05/01/2018
// Source: https://github.com/roboryantron/UnityEditorJunkie
// ----------------------------------------------------------------------------

using UnityEngine;

namespace MyBox
{
	using System;

	public enum SearchableEnumSorting
	{
		/// <summary>
		/// Sort by the underlying numerical value of this enum.
		/// I.e. <see cref="Enum.GetValues(Type)"/>.
		/// <br></br>
		/// This is the order Unity will give the list to us.
		/// </summary>
		Numerical,

		/// <summary>
		/// Sort by the enum name string. The exact contents of that name string are chosen by
		/// <see cref="SearchableEnumAttribute.Naming"/>
		/// </summary>
		Alphabetical,
	}

	public enum SearchableEnumNaming
	{
		/// <summary>
		/// This is the equivalent of <see cref=".Enum.ToString()"/>.
		/// </summary>
		Name,

		/// <summary>
		/// This is the equivalent of calling <see cref="UnityEditor.ObjectNames.NicifyVariableName"/> on the result of
		/// <see cref="Enum.ToString()"/>.
		/// </summary>
		DisplayName,

		NameAndValueDec,

		NameAndValueHex,
	}

	/// <summary>
	/// Put this attribute on a public (or SerializeField) enum in a
	/// MonoBehaviour or ScriptableObject to get an improved enum selector
	/// popup. The enum list is scrollable and can be filtered by typing.
	/// </summary>
	public class SearchableEnumAttribute : PropertyAttribute
	{
		/// <summary>Control how the entries in the list are displayed.</summary>
		public SearchableEnumNaming Naming = SearchableEnumNaming.Name;

		/// <summary>Control how the list is sorted.</summary>
		public SearchableEnumSorting Sorting = SearchableEnumSorting.Alphabetical;
	}
}

#if UNITY_EDITOR
namespace MyBox.EditorTools
{
	using UnityEditor;

	/// <summary>
	/// Base class to easily create searchable enum types
	/// </summary>
	public class SearchableEnumDrawer : PropertyDrawer
	{
		private Internal.SearchableEnumAttributeDrawer _drawer;
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (_drawer == null) _drawer = new Internal.SearchableEnumAttributeDrawer();
			GUIContent content = new GUIContent(property.displayName);
			Rect drawerRect = EditorGUILayout.GetControlRect(true, _drawer.GetPropertyHeight(property, content));
			_drawer.OnGUI(drawerRect, property, content);
		}
	}
}

namespace MyBox.Internal
{
	using System;
	using UnityEditor;
	using UnityEngine.UIElements;

	/// <summary>
	/// Draws the custom enum selector popup for enum fields using the
	/// SearchableEnumAttribute.
	/// </summary>
	[CustomPropertyDrawer(typeof(SearchableEnumAttribute))]
	public class SearchableEnumAttributeDrawer : PropertyDrawer
	{
		private const string TYPE_ERROR = "SearchableEnum can only be used on enum fields.";

		/// <summary>
		/// Cache of the hash to use to resolve the ID for the drawer.
		/// </summary>
		private int idHash;

		private Type _enumType => fieldInfo.FieldType;

		public override VisualElement CreatePropertyGUI(SerializedProperty property) => base.CreatePropertyGUI(property);

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			// If this is not used on an enum, show an error
			if (property.type != "Enum")
			{
				GUIStyle errorStyle = "CN EntryErrorIconSmall";
				Rect r = new Rect(position);
				r.width = errorStyle.fixedWidth;
				position.xMin = r.xMax;
				GUI.Label(r, "", errorStyle);
				GUI.Label(position, TYPE_ERROR);
				return;
			}

			// By manually creating the control ID, we can keep the ID for the
			// label and button the same. This lets them be selected together
			// with the keyboard in the inspector, much like a normal popup.
			if (idHash == 0) idHash = "SearchableEnumAttributeDrawer".GetHashCode();
			int id = GUIUtility.GetControlID(idHash, FocusType.Keyboard, position);

			label = EditorGUI.BeginProperty(position, label, property);
			position = EditorGUI.PrefixLabel(position, id, label);

			var searchableEnum = (SearchableEnumAttribute)attribute;

			var values = Enum.GetValues(_enumType);
			string[] list;
			switch (searchableEnum.Naming)
			{
				default:
				case SearchableEnumNaming.Name:
				{
					list = property.enumNames;
					break;
				}
				case SearchableEnumNaming.DisplayName:
				{
					list = property.enumDisplayNames;
					break;
				}
				case SearchableEnumNaming.NameAndValueDec:
				{
					var names = property.enumNames;
					list = new string[values.Length];
					for (int i = 0; i < values.Length; i++)
					{
						var value = values.GetValue(i);
						var decString = Enum.Format(_enumType, value, "d");
						list[i] = $"{names[i]}, {decString}";
					}
					break;
				}
				case SearchableEnumNaming.NameAndValueHex:
				{
					var names = property.enumNames;
					list = new string[values.Length];
					for (int i = 0; i < values.Length; i++)
					{
						var value = values.GetValue(i);
						var hexString = Enum.Format(_enumType, value, "x");
						var decString = Enum.Format(_enumType, value, "d");
						list[i] = $"{names[i]}, 0x{hexString}, {decString}";
					}
					break;
				}
			}
			
			var lookupListToProperty = new int[list.Length];
			for (int i = 0; i < lookupListToProperty.Length; i++)
			{
				lookupListToProperty[i] = i;
			}

			switch (searchableEnum.Sorting)
			{
				default:
				case SearchableEnumSorting.Alphabetical:
				{
					Array.Sort(list, lookupListToProperty);
					break;
				}
				case SearchableEnumSorting.Numerical:
				{
					// Already sorted this way.
					break;
				}
			}

			var lookupPropertyToList = new int[list.Length];
			for (int i = 0; i < lookupPropertyToList.Length; i++)
			{
				lookupPropertyToList[lookupListToProperty[i]] = i;
			}

			int Index() => lookupPropertyToList[property.enumValueIndex];
			void SetIndex(int i) => property.enumValueIndex = lookupListToProperty[i];

			GUIContent buttonText = new GUIContent(list[Index()]);
			if (DropdownButton(id, position, buttonText))
			{
				Action<int> onSelect = i =>
				{
					SetIndex(i);
					//property.enumValueFlag = (int)values.GetValue(i);
					property.serializedObject.ApplyModifiedProperties();
				};

				SearchablePopup.Show(position, list,
					Index(), onSelect);
			}

			EditorGUI.EndProperty();
		}

		/// <summary>
		/// A custom button drawer that allows for a controlID so that we can
		/// sync the button ID and the label ID to allow for keyboard
		/// navigation like the built-in enum drawers.
		/// </summary>
		private static bool DropdownButton(int id, Rect position, GUIContent content)
		{
			Event current = Event.current;
			switch (current.type)
			{
				case EventType.MouseDown:
					if (position.Contains(current.mousePosition) && current.button == 0)
					{
						Event.current.Use();
						return true;
					}

					break;
				case EventType.KeyDown:
					if (GUIUtility.keyboardControl == id && current.character == '\n')
					{
						Event.current.Use();
						return true;
					}

					break;
				case EventType.Repaint:
					EditorStyles.popup.Draw(position, content, id, false);
					break;
			}

			return false;
		}
	}
}
#endif
