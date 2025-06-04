using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Popups
{
	public class ColumnVisibilityPopupWindowContent : AnchoredPopupWindowContent
	{
		private readonly MultiColumnHeaderState m_MultiColumnHeaderState;
		private readonly int m_VisibleColumnLimit;
		private readonly Action<int[]> m_VisibilityChanged;

		private readonly bool[] m_ColumnVisibility;
		private readonly string[] m_ColumnNames;

		private Vector2 m_ScrollPos;

		public ColumnVisibilityPopupWindowContent(Rect anchoredRect, MultiColumnHeaderState multiColumnHeaderState, int visibleColumnLimit, Action<int[]> visibilityChanged) : base(anchoredRect)
		{
			m_MultiColumnHeaderState = multiColumnHeaderState;
			m_VisibleColumnLimit = visibleColumnLimit;
			m_VisibilityChanged = visibilityChanged;

			m_ColumnVisibility = new bool[m_MultiColumnHeaderState.columns.Length];
			m_ColumnNames = new string[m_MultiColumnHeaderState.columns.Length];

			for (var i = 0; i < m_MultiColumnHeaderState.columns.Length; i++)
			{
				m_ColumnVisibility[i] = m_MultiColumnHeaderState.visibleColumns.Contains(i);
				m_ColumnNames[i] = string.IsNullOrEmpty(m_MultiColumnHeaderState.columns[i].headerContent?.text) ? i.ToString() : m_MultiColumnHeaderState.columns[i].headerContent.text;
			}
		}

		public override Vector2 GetWindowSize()
		{
			// Can't call GUI.skin if opening via context menu.
			if (Event.current == null || m_ColumnNames.Length <= 0)
			{
				return m_AnchoredRect.size;
			}

			var widestLabel = m_ColumnNames.Max(n => GUI.skin.toggle.CalcSize(new GUIContent(n)).x);
			var widthBestFit = Mathf.Min(widestLabel + PopupContent.Window.ColumnVisibilityPadding, m_AnchoredRect.size.x);

			var contentHeight = (m_ColumnVisibility.Length + 1) * EditorGUIUtility.singleLineHeight;
			var heightBestFit = Mathf.Min(contentHeight + PopupContent.Window.ColumnVisibilityPadding, m_AnchoredRect.size.y);

			return new Vector2(widthBestFit, heightBestFit);
		}

		public override void OnGUI()
		{
			if (m_ColumnVisibility.Length <= 0)
			{
				return;
			}

			var columnVisibilityHeaderContent = SheetsContent.Label.GetColumnContent(m_ColumnVisibility.Count(c => c == true), m_VisibleColumnLimit, m_ColumnVisibility.Count());
			columnVisibilityHeaderContent.text = $"{PopupContent.Label.ColumnVisibility} {columnVisibilityHeaderContent.text}";
			EditorGUILayout.LabelField(columnVisibilityHeaderContent);

			m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);

			EditorGUI.BeginChangeCheck();

			var visibleColumns = new List<int>();
			for (var i = 0; i < m_ColumnVisibility.Length; i++)
			{
				EditorGUI.BeginDisabledGroup((m_MultiColumnHeaderState.visibleColumns.Length >= m_VisibleColumnLimit && !m_ColumnVisibility[i]) || i == 0);
				m_ColumnVisibility[i] = EditorGUILayout.ToggleLeft(m_ColumnNames[i], m_ColumnVisibility[i]);
				if (m_ColumnVisibility[i])
				{
					visibleColumns.Add(i);
				}
				EditorGUI.EndDisabledGroup();
			}

			if (EditorGUI.EndChangeCheck())
			{
				m_VisibilityChanged?.Invoke(visibleColumns.ToArray());
			}

			EditorGUILayout.EndScrollView();
		}
	}
}