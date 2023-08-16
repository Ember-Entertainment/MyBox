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

		/// <summary>
		/// Sort by the order the enum values are declared.
		/// </summary>
		DeclarationOrder,
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

		/// <summary>
		/// The same as <see cref="Name"/> with the value displayed as a decimal integer afterwards.
		/// </summary>
		NameAndValueDec,
		

		/// <summary>
		/// The same as <see cref="Name"/> with the value displayed as a hexadecimal integer and then as a decimal
		/// integer afterwards.
		/// </summary>
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
		
		public SearchableEnumAttribute(
			SearchableEnumNaming naming = SearchableEnumNaming.Name,
			SearchableEnumSorting sorting = SearchableEnumSorting.Alphabetical
		)
		{
			Naming = naming;
			Sorting = sorting;
		}
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
	using System.Collections.Generic;
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

		/// <summary>
		/// For caching the sorting and naming of each enum type to avoid doing it in every OnGUI call.
		/// </summary>
		private static Dictionary<EnumListDataKey, EnumListData> _enumListData = new Dictionary<EnumListDataKey, EnumListData>();

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

			// memoize the sorting and naming for this enum type
			var enumListDataKey = new EnumListDataKey()
			{
				EnumType = _enumType,
				Attribute = (SearchableEnumAttribute)attribute,
			};
			if (!_enumListData.ContainsKey(enumListDataKey))
			{
				_enumListData.Add(enumListDataKey, new EnumListData(enumListDataKey.EnumType, property, enumListDataKey.Attribute));
			}
			var enumListData = _enumListData[enumListDataKey];

			var dropdownIndex = enumListData.PropertyIndexToDropdownIndex(property.enumValueIndex);
			GUIContent buttonText = new GUIContent(enumListData.DropdownValues[dropdownIndex]);
			if (DropdownButton(id, position, buttonText))
			{
				Action<int> onSelect = i =>
				{
					property.enumValueIndex = enumListData.DropdownIndexToPropertyIndex(i);
					property.serializedObject.ApplyModifiedProperties();
				};

				SearchablePopup.Show(position, enumListData.DropdownValues,
					dropdownIndex, onSelect);
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
		
		private class EnumListDataKey : IEquatable<EnumListDataKey>
		{
			public Type EnumType;
			public SearchableEnumAttribute Attribute;

			public bool Equals(EnumListDataKey other)
			{
				if (other == null)
				{
					return false;
				}
				if (EnumType != other.EnumType)
				{
					return false;
				}
				if (Attribute.Naming != other.Attribute.Naming)
				{
					return false;
				}
				if (Attribute.Sorting != other.Attribute.Sorting)
				{
					return false;
				}
				return true;
			}

			public override int GetHashCode() => EnumType.GetHashCode() + Attribute.Naming.GetHashCode() << 2 + Attribute.Sorting.GetHashCode() << 4;
		}

		/// <summary>
		/// A class with all the data for sorting an enum any way we want.
		/// </summary>
		private class EnumListEntry
		{
			public string Name;

			/// <summary>
			/// The value of the <see cref="Enum"/> entry.
			/// Using a <see cref="long"/> to support any kind of underlying type for the <see cref="Enum"/>.
			/// </summary>
			public long Value;
			public int EnumIndex;
			public int PropertyIndex;
		}

		private class EnumListEntryCompareByName : IComparer<EnumListEntry>
		{
			public int Compare(EnumListEntry self, EnumListEntry other)
			{
				return self.Name.CompareTo(other.Name);
			}
		}

		private class EnumListEntryCompareByValue : IComparer<EnumListEntry>
		{
			public int Compare(EnumListEntry self, EnumListEntry other)
			{
				return self.Value.CompareTo(other.Value);
			}
		}

		private class EnumListEntryCompareByEnumIndex : IComparer<EnumListEntry>
		{
			public int Compare(EnumListEntry self, EnumListEntry other)
			{
				return self.EnumIndex.CompareTo(other.EnumIndex);
			}
		}

		private class EnumListEntryCompareByPropertyIndex : IComparer<EnumListEntry>
		{
			public int Compare(EnumListEntry self, EnumListEntry other)
			{
				return self.PropertyIndex.CompareTo(other.PropertyIndex);
			}
		}

		private class EnumListData
		{
			public Type EnumType;
			public string[] DropdownValues;

			private int[] _propertyIndexToDropdownIndex;

			// Maybe this could be shared if there are places that sort or name the same enum differently.
			// For now I'm not too worried about it since it can only happen once per EnumListDataKey.
			private EnumListEntry[] _sortedList;

			public int DropdownIndexToPropertyIndex(int i) => _sortedList[i].PropertyIndex;

			public int PropertyIndexToDropdownIndex(int i) => _propertyIndexToDropdownIndex[i];

			public EnumListData(Type enumType, SerializedProperty property, SearchableEnumAttribute searchableEnum)
			{
				EnumType = enumType;

				var values = Enum.GetValues(EnumType);
				DropdownValues = new string[values.Length];
				_sortedList = new EnumListEntry[values.Length];

				string[] names = null;
				switch (searchableEnum.Naming)
				{
					default:
					case SearchableEnumNaming.Name:
					case SearchableEnumNaming.NameAndValueDec:
					case SearchableEnumNaming.NameAndValueHex:
						names = property.enumNames;
						break;
					case SearchableEnumNaming.DisplayName:
						names = property.enumDisplayNames;
						break;
				}

				for (int i = 0; i < values.Length; i++)
				{
					string name;
					var value = values.GetValue(i);

					switch (searchableEnum.Naming)
					{
						default:
						case SearchableEnumNaming.Name:
						case SearchableEnumNaming.DisplayName:
						{
							name = names[i];
							break;
						}
						case SearchableEnumNaming.NameAndValueDec:
						{
							var decString = Enum.Format(EnumType, value, "d");
							name = $"{names[i]}, {decString}";
							break;
						}
						case SearchableEnumNaming.NameAndValueHex:
						{
							var hexString = Enum.Format(EnumType, value, "x");
							var decString = Enum.Format(EnumType, value, "d");
							name = $"{names[i]}, 0x{hexString}, {decString}";
							break;
						}
					}

					_sortedList[i] = new EnumListEntry()
					{
						Name = name,
						Value = Convert.ToInt64(value),
						EnumIndex = EnumType.GetField(names[i]).MetadataToken,
						PropertyIndex = i,
					};
				}

				IComparer<EnumListEntry> comparer = null;
				switch (searchableEnum.Sorting)
				{
					default:
					case SearchableEnumSorting.Alphabetical:
						comparer = new EnumListEntryCompareByName();
						break;
					case SearchableEnumSorting.Numerical:
						comparer = new EnumListEntryCompareByValue();
						break;
					case SearchableEnumSorting.DeclarationOrder:
						comparer = new EnumListEntryCompareByEnumIndex();
						break;
				}

				Array.Sort(_sortedList, comparer);

				DropdownValues = new string[_sortedList.Length];
				_propertyIndexToDropdownIndex = new int[_sortedList.Length];
				for (int i = 0; i < _sortedList.Length; i++)
				{
					DropdownValues[i] = _sortedList[i].Name;
					var propertyIndex = DropdownIndexToPropertyIndex(i);
					_propertyIndexToDropdownIndex[propertyIndex] = i;
				}
			}
		}
	}
}
#endif
