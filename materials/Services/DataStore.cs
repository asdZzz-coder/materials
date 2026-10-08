using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using materials.Models;

namespace materials.Services
{
    /// <summary>存檔內容：物料清單 + 分類清單（含還沒放物料的空分類）+ 預設單位清單 + 出入庫紀錄與借出單。</summary>
    public class MaterialData
    {
        public List<MaterialItem> Items { get; set; } = new();
        public List<string> Categories { get; set; } = new();

        /// <summary>預設單位；舊版存檔沒有這個欄位時為 null，由 UnitService.Clean 補上預設清單。</summary>
        public List<string>? Units { get; set; }

        /// <summary>出入庫紀錄（存入、拿出、借出、歸還），依時間先後。</summary>
        public List<StockRecord> Records { get; set; } = new();

        /// <summary>借出單（含已還清的）。</summary>
        public List<Loan> Loans { get; set; } = new();
    }

    /// <summary>
    /// 物料資料存成 JSON（%AppData%\MaterialsKeeper\materials.json），不加密，可直接備份或用記事本查看。
    /// 寫入時先寫暫存檔再換名，存到一半當機也不會把舊資料弄壞。換電腦時請用「匯出 Excel → 匯入」搬資料。
    /// </summary>
    public static class DataStore
    {
        // 環境變數 MATERIALSKEEPER_DATA_DIR 可指定其他資料夾（測試用）；平常不設定，存在 %AppData%\MaterialsKeeper
        public static readonly string DataDirectory =
            Environment.GetEnvironmentVariable("MATERIALSKEEPER_DATA_DIR")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MaterialsKeeper");

        public const string FileName = "materials.json";

        private static string FilePath => Path.Combine(DataDirectory, FileName);

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 中文直接存成中文，用記事本打開也看得懂
        };

        public static MaterialData Load() => Load(FilePath);

        public static void Save(MaterialData data) => Save(FilePath, data);

        internal static MaterialData Load(string path)
        {
            if (!File.Exists(path)) return new();
            try
            {
                return JsonSerializer.Deserialize<MaterialData>(File.ReadAllText(path), Options) ?? new();
            }
            catch (JsonException)
            {
                // 檔案損毀（例如被手動改壞）：備份後以空資料開始，不覆蓋原檔
                File.Move(path, path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
                return new();
            }
        }

        internal static void Save(string path, MaterialData data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, Options));
            File.Move(tmp, path, overwrite: true);
        }
    }
}
