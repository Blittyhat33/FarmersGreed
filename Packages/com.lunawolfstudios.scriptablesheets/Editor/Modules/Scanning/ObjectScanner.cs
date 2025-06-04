using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using LunaWolfStudiosEditor.ScriptableSheets.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LunaWolfStudiosEditor.ScriptableSheets.Scanning
{
	public class ObjectScanner
	{
		private const string ProgressBarTitle = "Scriptable Sheets - Object Scanner";

		private readonly List<Object> m_Objects = new List<Object>();
		public List<Object> Objects => m_Objects;

		private Type[] m_ObjectTypes;
		public Type[] ObjectTypes => m_ObjectTypes;

		private string[] m_ObjectTypeNames;
		public string[] ObjectTypeNames => m_ObjectTypeNames;

		private string[] m_FriendlyObjectTypeNames;
		public string[] FriendlyObjectTypeNames => m_FriendlyObjectTypeNames;

		private readonly Dictionary<Type, Dictionary<Object, List<Object>>> m_SubAssetsByTypeAndMainAsset = new Dictionary<Type, Dictionary<Object, List<Object>>>();
		public Dictionary<Type, Dictionary<Object, List<Object>>> SubAssetsByTypeAndMainAsset => m_SubAssetsByTypeAndMainAsset;

		public void ScanObjects(ScanSettings settings, SheetAsset sheetAsset)
		{
			try
			{
				m_Objects.Clear();
				m_SubAssetsByTypeAndMainAsset.Clear();

				// Refresh incase new folders or assets were created.
				AssetDatabase.Refresh();

				// Unity does not consider the root Packages folder as a valid folder so we need to check it separately.
				if (settings.PathOption == ScanPathOption.Default && !AssetDatabase.IsValidFolder(settings.Path) && settings.Path != UnityConstants.Packages)
				{
					settings.Path = UnityConstants.DefaultAssetPath;
				}

				var scanPaths = settings.GetScanPaths();
				var joinedPaths = settings.GetJoinedScanPaths(scanPaths);

				if (settings.ShowProgressBar)
				{
					EditorUtility.DisplayProgressBar(ProgressBarTitle, $"Scanning for assets of type {sheetAsset} at path(s) {joinedPaths}", 0.40f);
				}

				var guids = AssetDatabase.FindAssets($"t:{sheetAsset}", scanPaths);

				if (settings.ShowProgressBar)
				{
					EditorUtility.DisplayProgressBar(ProgressBarTitle, $"Loading assets of type {sheetAsset} at path(s) {joinedPaths}", 0.80f);
				}

				foreach (var guid in guids)
				{
					var path = AssetDatabase.GUIDToAssetPath(guid);
					var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
					if (asset != null)
					{
						m_Objects.Add(asset);

						if (sheetAsset != SheetAsset.Scene)
						{
							var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
							if (subAssets.Length > 1)
							{
								foreach (var subAsset in subAssets)
								{
									if (subAsset != null && subAsset != asset)
									{
										if (sheetAsset == SheetAsset.Prefab)
										{
											if (subAsset is Component)
											{
												var subAssetGameObject = ((Component) subAsset).gameObject;
												var isRootPrefab = asset == subAssetGameObject;
												if (!settings.RootPrefabsOnly || isRootPrefab)
												{
													m_Objects.Add(subAsset);
												}
											}
										}
										else
										{
											if (AssetDatabase.IsSubAsset(subAsset))
											{
												if (sheetAsset != SheetAsset.ScriptableObject || typeof(ScriptableObject).IsAssignableFrom(subAsset.GetType()))
												{
													m_Objects.Add(subAsset);
												}
												var subAssetType = subAsset.GetType();
												if (!m_SubAssetsByTypeAndMainAsset.TryGetValue(subAssetType, out var subAssetByMainAsset))
												{
													subAssetByMainAsset = new Dictionary<Object, List<Object>>();
													m_SubAssetsByTypeAndMainAsset[subAssetType] = subAssetByMainAsset;
												}
												if (!subAssetByMainAsset.TryGetValue(asset, out var subAssetList))
												{
													subAssetList = new List<Object>();
													subAssetByMainAsset[asset] = subAssetList;
												}
												subAssetList.Add(subAsset);
											}
										}
									}
								}
							}
						}
					}
				}

				if (settings.ShowProgressBar)
				{
					EditorUtility.DisplayProgressBar(ProgressBarTitle, $"Resolving {sheetAsset} types using {nameof(ScanOption)} {settings.Option}", 0.90f);
				}

				if (sheetAsset == SheetAsset.ScriptableObject && settings.Option == ScanOption.Assembly)
				{
					var assemblies = AppDomain.CurrentDomain.GetAssemblies();
					var objectTypes = new List<Type>();
					foreach (var assembly in assemblies)
					{
						var types = assembly.GetTypes().Where
						(
							type => type.IsSubclassOf(typeof(ScriptableObject))
								&& type.IsSerializable
								&& !type.IsAbstract
								&& !type.IsGenericType
								&& !type.IsNested
								&& !type.IsSubclassOf(typeof(Editor))
								&& !type.IsSubclassOf(typeof(EditorWindow))
						);
						objectTypes.AddRange(types);
					}
					m_ObjectTypes = objectTypes.OrderBy(type => type.FullName).ToArray();
				}
				else
				{
					m_ObjectTypes = m_Objects.Select(o => o.GetType()).Distinct().OrderBy(type => type.FullName).ToArray();
				}

				if (settings.ShowProgressBar)
				{
					EditorUtility.DisplayProgressBar(ProgressBarTitle, $"Finalizing {sheetAsset} type names", 0.98f);
				}

				m_ObjectTypeNames = m_ObjectTypes.Select(type => type.FullName).ToArray();
				// Submenu once we start having a lot of Objects.
				var separator = '.';
				if (m_ObjectTypeNames.Length > SheetLayout.SubMenuThreshold)
				{
					var newSeparator = '/';
					m_ObjectTypeNames = m_ObjectTypeNames.Select(s => s.Replace(separator, newSeparator)).ToArray();
					separator = newSeparator;
				}
				m_FriendlyObjectTypeNames = m_ObjectTypeNames.Select(s => s.Substring(s.LastIndexOf(separator) + 1)).ToArray();
			}
			catch (Exception ex)
			{
				Debug.LogError($"{nameof(ScanObjects)} for {nameof(SheetAsset)} {sheetAsset} failed with error: {ex}");
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}
	}
}
