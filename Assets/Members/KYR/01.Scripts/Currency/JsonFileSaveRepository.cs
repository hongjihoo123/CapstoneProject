using System;
using System.IO;
using UnityEngine;

namespace Members.KYR._01.Scripts.Currency
{
    public class JsonFileSaveRepository : ISaveRepository
    {
        private readonly string _path;
        private readonly string _tmpPath;
        private readonly string _bakPath;

        public JsonFileSaveRepository(string fileName = "currency.json")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
            _tmpPath = _path + ".tmp";
            _bakPath = _path + ".bak";
        }

        public CurrencySaveData Load()
        {
            return TryRead(_path) ?? TryRead(_bakPath) ?? new CurrencySaveData();
        }

        public void Save(CurrencySaveData data)
        {
            try
            {
                File.WriteAllText(_tmpPath, JsonUtility.ToJson(data));

                if (File.Exists(_path))
                    File.Replace(_tmpPath, _path, _bakPath);
                else
                    File.Move(_tmpPath, _path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 저장 실패: {e}");
            }
        }

        private static CurrencySaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                return JsonUtility.FromJson<CurrencySaveData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 읽기 실패 ({path}): {e.Message}");
                return null;
            }
        }
    }
}
