using LunaWolfStudiosEditor.ScriptableSheets.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Tables
{
	public static class TableUtility
	{
		public static void FromFlatFileFormat(this Table<ITableProperty> propertyTable, string flatFileContent, FlatFileFormatSettings formatSettings)
		{
			var wrapper = formatSettings.GetWrapper();
			if (formatSettings.HasHeaders)
			{
				var firstRowEndIndex = flatFileContent.IndexOf(formatSettings.RowDelimiter);
				var joinedColumnHeaders = formatSettings.GetJoinedColumnHeaders(wrapper);
				if (firstRowEndIndex >= 0)
				{
					var headerRow = flatFileContent.Substring(0, firstRowEndIndex).Trim();
					// Validate column headers match the header row.
					if (joinedColumnHeaders.Contains(headerRow) || headerRow.Contains(joinedColumnHeaders))
					{
						flatFileContent = flatFileContent.Substring(firstRowEndIndex + formatSettings.RowDelimiter.Length);
					}
				}
				else
				{
					// Handle case where there's only a single row.
					if (joinedColumnHeaders.Contains(flatFileContent))
					{
						flatFileContent = string.Empty;
					}
				}
			}
			var stringSplitOptions = formatSettings.RemoveEmptyRows ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None;
			string[] rowData;
			if (formatSettings.HasWrapping)
			{
				var inverseWrap = wrapper.WrapInverse(formatSettings.RowDelimiter);
				// Replace any trailing white space at end of line.
				flatFileContent = Regex.Replace(flatFileContent, $@"{Regex.Escape(wrapper.Close.ToString())}\s*{Regex.Escape(formatSettings.RowDelimiter)}\s*{Regex.Escape(wrapper.Open.ToString())}", inverseWrap);
				// Use new string array parameter to support older versions of C#.
				rowData = flatFileContent.Split(new string[] { inverseWrap }, stringSplitOptions);
				for (var i = 0; i < rowData.Length - 1; i++)
				{
					rowData[i] += wrapper.Close;
					var nextRow = i + 1;
					if (nextRow < rowData.Length)
					{
						rowData[nextRow] = wrapper.Open + rowData[nextRow];
					}
				}
				var totalWrapChars = flatFileContent.Count(c => c == wrapper.Open || c == wrapper.Close);
				var expectedWrapChars = rowData.Length * 2;
				if (totalWrapChars < expectedWrapChars)
				{
					Debug.LogWarning($"Invalid {nameof(WrapOption)} {formatSettings.WrapOption}. Found {totalWrapChars} matching char(s) but expected at least {expectedWrapChars}. Defaulting to {nameof(WrapOption)} {WrapOption.None}.");
					formatSettings.WrapOption = WrapOption.None;
					rowData = flatFileContent.Split(new string[] { formatSettings.RowDelimiter }, stringSplitOptions);
				}
			}
			else
			{
				rowData = flatFileContent.Split(new string[] { formatSettings.RowDelimiter }, stringSplitOptions);
			}
			var rowIndex = 0;
			for (var row = formatSettings.FirstRowIndex; rowIndex < rowData.Length && row < propertyTable.Rows; row++)
			{
				string[] columnData;
				if (formatSettings.HasWrapping)
				{
					var line = rowData[rowIndex].Trim();
					columnData = line.Split(new string[] { wrapper.WrapInverse(formatSettings.ColumnDelimiter) }, StringSplitOptions.None);
					if (columnData.Length > 0 && columnData[0].StartsWith(wrapper.Open.ToString()))
					{
						columnData[0] = columnData[0].Remove(0, 1);
						var lastIndex = columnData.Length - 1;
						if (columnData[lastIndex].EndsWith(wrapper.Close.ToString()))
						{
							columnData[lastIndex] = columnData[lastIndex].Remove(columnData[lastIndex].Length - 1, 1);
						}
					}
				}
				else
				{
					columnData = rowData[rowIndex].Split(new string[] { formatSettings.ColumnDelimiter }, StringSplitOptions.None);
				}
				var columnIndex = 0;
				if (formatSettings.HasWrapping && formatSettings.EscapeOption != EscapeOption.None)
				{
					var escapedOpen = formatSettings.EscapeOption.GetEscapedWrapper(wrapper.Open, formatSettings.CustomEscapeSequence);
					var escapedClose = formatSettings.EscapeOption.GetEscapedWrapper(wrapper.Close, formatSettings.CustomEscapeSequence);
					for (var y = formatSettings.FirstColumnIndex; columnIndex < columnData.Length && y < propertyTable.Columns; y++)
					{
						var unescapedValue = wrapper.UnescapeContent(columnData[columnIndex], escapedOpen, escapedClose);
						propertyTable.UpdateProperty(row, y, unescapedValue, formatSettings);
						columnIndex++;
					}
				}
				else
				{
					for (var y = formatSettings.FirstColumnIndex; columnIndex < columnData.Length && y < propertyTable.Columns; y++)
					{
						propertyTable.UpdateProperty(row, y, columnData[columnIndex], formatSettings);
						columnIndex++;
					}
				}
				rowIndex++;
			}
		}

		private static ITableProperty UpdateProperty(this Table<ITableProperty> propertyTable, int row, int column, string value, FlatFileFormatSettings formatSettings)
		{
			var property = propertyTable.Get(row, column);
			// Property could be null when mixing array sizes.
			if (property != null)
			{
				property.SetProperty(value, formatSettings);
			}
			return property;
		}

		public static void FromJsonFormat(this Table<ITableProperty> propertyTable, string json, JsonSerializationFormat format, FlatFileFormatSettings formatSettings)
		{
			switch (format)
			{
				case JsonSerializationFormat.Flat:
					propertyTable.FromJsonFlatFormat(json, formatSettings);
					break;

				case JsonSerializationFormat.Hierarchy:
					propertyTable.FromJsonHierarchyFormat(json, formatSettings);
					break;

				default:
					Debug.LogError($"Unsupported {nameof(JsonSerializationFormat)} {format}.");
					break;
			}
		}

		private static void FromJsonFlatFormat(this Table<ITableProperty> propertyTable, string json, FlatFileFormatSettings formatSettings)
		{
			try
			{
				var rowData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
				var row = 0;
				foreach (var rowEntry in rowData)
				{
					if (row >= propertyTable.Rows)
					{
						LogJSONRowWarning(rowData.Count, propertyTable.Rows);
						break;
					}
					var column = formatSettings.FirstColumnIndex;
					var rowValues = rowEntry.Value.ToArray();
					for (var i = 0; i < rowValues.Length; i++)
					{
						if (column >= propertyTable.Columns)
						{
							// Add 1 for Actions column.
							Debug.LogWarning($"Mismatched columns. JSON data has ({rowEntry.Value.Count + 1}) column(s) but the property table only has ({propertyTable.Columns}) column(s).");
							break;
						}
						var property = propertyTable.UpdateProperty(row, column, rowValues[i].Value, formatSettings);
						// Only go to the next element if the previous was found successfully. This handles edge cases with mismatched array sizes.
						if (property == null)
						{
							i--;
						}
						else if (property.PropertyPath != rowValues[i].Key)
						{
							Debug.LogWarning($"Mismatched property path. Expected property path '{property.PropertyPath}' but used property path from JSON '{rowValues[i].Key}' for '{rowEntry.Key}'.");
						}
						column++;
					}
					row++;
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"Error parsing JSON {ex.Message}");
			}
		}

		private static void FromJsonHierarchyFormat(this Table<ITableProperty> propertyTable, string json, FlatFileFormatSettings formatSettings)
		{
			try
			{
				var rowData = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(json);
				var row = 0;
				foreach (var rowEntry in rowData)
				{
					if (row >= propertyTable.Rows)
					{
						LogJSONRowWarning(rowData.Count, propertyTable.Rows);
						break;
					}
					var rootObject = propertyTable.Get(row, formatSettings.FirstColumnIndex).RootObject;
					var assetName = rowEntry.Key;
					if (assetName != rootObject.name)
					{
						var assetPath = AssetDatabase.GetAssetPath(rootObject);
						var assetRenameResponse = AssetDatabase.RenameAsset(assetPath, assetName);
						if (!string.IsNullOrEmpty(assetRenameResponse))
						{
							Debug.LogWarning($"Cannot rename {rootObject.name} at asset path {assetPath} to new name '{assetName.GetEscapedText()}'.\n{assetRenameResponse}");
							assetName = rootObject.name;
						}
					}
					var serializedObjectJson = JsonConvert.SerializeObject(rowEntry.Value);
					if (rootObject is ScriptableObject)
					{
						JsonUtility.FromJsonOverwrite(serializedObjectJson, rootObject);
					}
					else
					{
						EditorJsonUtility.FromJsonOverwrite(serializedObjectJson, rootObject);
						// Ensure m_Name property matches filename after deserializing.
						rootObject.name = assetName;
					}
					EditorUtility.SetDirty(rootObject);
					row++;
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"Error parsing JSON {ex.Message}");
			}
		}

		private static void LogJSONRowWarning(int expectedRows, int actualRows)
		{
			Debug.LogWarning($"Mismatched rows. JSON data has ({expectedRows}) row(s) but the property table only has ({actualRows}) row(s). Create ({expectedRows - actualRows}) more row(s).");
		}

		public static string ToFlatFileFormat(this Table<ITableProperty> propertyTable, FlatFileFormatSettings formatSettings)
		{
			var lastColumn = propertyTable.Columns - 1;
			var lastRow = propertyTable.Rows - 1;
			var flatFile = new StringBuilder();
			var wrapper = formatSettings.GetWrapper();
			if (formatSettings.HasHeaders)
			{
				for (var column = formatSettings.FirstColumnIndex; column < formatSettings.ColumnHeaders.Length; column++)
				{
					var header = formatSettings.ColumnHeaders[column];
					if (formatSettings.HasWrapping)
					{
						flatFile.Wrap(header, wrapper);
					}
					else
					{
						flatFile.Append(header);
					}
					if (formatSettings.FirstColumnOnly)
					{
						break;
					}
					if (column < lastColumn)
					{
						flatFile.Append(formatSettings.ColumnDelimiter);
					}
				}
				if (propertyTable.Rows > formatSettings.FirstRowIndex)
				{
					flatFile.Append(formatSettings.RowDelimiter);
				}
			}
			for (var row = formatSettings.FirstRowIndex; row < propertyTable.Rows; row++)
			{
				for (var column = formatSettings.FirstColumnIndex; column < propertyTable.Columns; column++)
				{
					var property = propertyTable.Get(row, column)?.GetProperty(formatSettings);
					if (formatSettings.HasWrapping)
					{
						if (formatSettings.EscapeOption != EscapeOption.None)
						{
							var escapedOpen = formatSettings.EscapeOption.GetEscapedWrapper(wrapper.Open, formatSettings.CustomEscapeSequence);
							var escapedClose = formatSettings.EscapeOption.GetEscapedWrapper(wrapper.Close, formatSettings.CustomEscapeSequence);
							property = wrapper.EscapeContent(property, escapedOpen, escapedClose);
						}
						flatFile.Wrap(property, wrapper);
					}
					else
					{
						flatFile.Append(property);
					}
					if (formatSettings.FirstColumnOnly)
					{
						break;
					}
					if (column < lastColumn)
					{
						flatFile.Append(formatSettings.ColumnDelimiter);
					}
				}
				if (formatSettings.FirstRowOnly)
				{
					break;
				}
				if (row < lastRow)
				{
					flatFile.Append(formatSettings.RowDelimiter);
				}
			}
			return flatFile.ToString();
		}

		public static string ToJsonFormat(this Table<ITableProperty> propertyTable, JsonSerializationFormat format, FlatFileFormatSettings formatSettings)
		{
			switch (format)
			{
				case JsonSerializationFormat.Flat:
					return propertyTable.ToJsonFlatFormat(formatSettings);

				case JsonSerializationFormat.Hierarchy:
					return propertyTable.ToJsonHierarchyFormat();

				default:
					Debug.LogError($"Unsupported {nameof(JsonSerializationFormat)} {format}.");
					break;
			}
			return string.Empty;
		}

		private static string ToJsonFlatFormat(this Table<ITableProperty> propertyTable, FlatFileFormatSettings formatSettings)
		{
			var rowObjects = new Dictionary<string, Dictionary<string, string>>();
			for (var row = 0; row < propertyTable.Rows; row++)
			{
				if (propertyTable.Columns > formatSettings.FirstColumnIndex)
				{
					var rowRootObjectName = propertyTable.Get(row, formatSettings.FirstColumnIndex).RootObject.name;
					var rowData = new Dictionary<string, string>();
					for (var column = formatSettings.FirstColumnIndex; column < propertyTable.Columns; column++)
					{
						var value = propertyTable.Get(row, column);
						if (value != null)
						{
							rowData[value.PropertyPath] = value.GetProperty(formatSettings);
						}
					}
					rowObjects[rowRootObjectName] = rowData;
				}
			}
			return JsonConvert.SerializeObject(rowObjects, Formatting.Indented);
		}

		private static string ToJsonHierarchyFormat(this Table<ITableProperty> propertyTable)
		{
			var rowObjects = new Dictionary<string, JObject>();
			for (var row = 0; row < propertyTable.Rows; row++)
			{
				if (propertyTable.Columns > 1)
				{
					var rowRootObject = propertyTable.Get(row, 1).RootObject;
					var unityJson = rowRootObject is ScriptableObject ? JsonUtility.ToJson(rowRootObject) : EditorJsonUtility.ToJson(rowRootObject);
					rowObjects[rowRootObject.name] = (JObject) JsonConvert.DeserializeObject(unityJson);
				}
			}
			return JsonConvert.SerializeObject(rowObjects, Formatting.Indented);
		}
	}
}
