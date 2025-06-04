using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LunaWolfStudiosEditor.ScriptableSheets.Importers
{
	[HelpURL("https://github.com/LunaWolfStudios/ScriptableSheetsDocs/blob/main/DOCUMENTATION.md#google-sheets-importers")]
	[CreateAssetMenu(fileName = "NewGoogleSheetsImporter", menuName = "Scriptable Sheets/Google Sheets Importer")]
	public class GoogleSheetsImporter : ScriptableObject
	{
		[SerializeField]
		private string m_FullTypeName;
		public string FullTypeName { get => m_FullTypeName; set => m_FullTypeName = value; }

		[SerializeField]
		private MonoScript m_MonoScript;
		public MonoScript MonoScript { get => m_MonoScript; set => m_MonoScript = value; }

		[SerializeField]
		private Object m_MainAsset;
		public Object MainAsset { get => m_MainAsset; set => m_MainAsset = value; }

		[SerializeField]
		private string m_SheetId;
		public string SheetId { get => m_SheetId; set => m_SheetId = value; }

		[SerializeField]
		private string m_SheetName;
		public string SheetName { get => m_SheetName; set => m_SheetName = value; }

		public string Url => $"https://docs.google.com/spreadsheets/d/{m_SheetId}/gviz/tq?tqx=out:csv&sheet={m_SheetName}";

		public async Task<string> GetCsvDataAsync()
		{
			var sheetName = m_SheetName;
			using var httpClient = new HttpClient();
			try
			{
				Debug.Log($"Getting '{sheetName}' CSV data from '{Url}'.");
				var csvData = await httpClient.GetStringAsync(Url);
				return csvData;
			}
			catch (Exception e)
			{
				Debug.LogError($"Failed to get '{sheetName}' CSV data from '{Url}'.\n{e.Message}");
			}
			return string.Empty;
		}

		public bool IsTypeMatch(Type type, MonoScript monoScript)
		{
			return type.FullName == FullTypeName || (monoScript != null && monoScript == m_MonoScript);
		}
	}
}