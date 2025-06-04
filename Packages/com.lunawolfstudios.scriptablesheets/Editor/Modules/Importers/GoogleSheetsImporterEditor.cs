using UnityEditor;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Importers
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(GoogleSheetsImporter))]
	public class GoogleSheetsImporterEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var googleSheetsImporter = (GoogleSheetsImporter) target;
			DrawDefaultInspector();

			EditorGUILayout.Space();

			if (GUILayout.Button("Copy URL"))
			{
				EditorGUIUtility.systemCopyBuffer = googleSheetsImporter.Url;
			}

			if (GUILayout.Button("Debug CSV to Console"))
			{
				DebugCsvToConsoleAsync(googleSheetsImporter);
			}
		}

		private async void DebugCsvToConsoleAsync(GoogleSheetsImporter sheetData)
		{
			var csvData = await sheetData.GetCsvDataAsync();
			Debug.Log(csvData);
		}
	}
}