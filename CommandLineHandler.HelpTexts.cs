namespace BatchCompress.Avalonia;

// GLM-5.3, 2026-11-04 / Antigravity, 2026-11-07：
// 命令行多语言帮助文本定义（zh-CN, zh-TW, en, ja, de）。
// 拆分自 CommandLineHandler.cs（降低单文件行数，并补齐日德两语言帮助文本）。
public static partial class CommandLineHandler
{
    private const string HelpText =
        """
        批量压缩解压工具

        用法:
          BatchCompress.Avalonia [gui]
          BatchCompress.Avalonia compress -i <路径...> -o <输出目录> [选项]
          BatchCompress.Avalonia extract -i <归档...> -o <输出目录> [选项]

        模式:
          compress, --compress, -c       执行无界面批量压缩
          extract, decompress, --decompress, -d
                                        执行无界面批量解压
          gui, --gui, -g                 显式启动图形界面

        来源与输出:
          --source, -s <目录>            批处理来源目录，压缩时处理其直接子项
          --input, -i <路径...>          明确输入的文件或目录，可重复指定
          --output, -o <目录>            输出目录
          --text-file, -t <文件>         解压文件名与逐项密码 TXT 清单

        压缩参数:
          --extension, --format, -e      创建：rar、7z、zip、tar、gz、bz2、xz、wim；解压：7zz 可读取格式
          --level, -l <0..5>             压缩级别
          --solid / --no-solid           启用或关闭固实压缩
          --volume-size, -v <数字>       分卷数值
          --volume-unit <b|k|m|g|t>      分卷单位
          --recovery <0..100>            RAR 恢复记录百分比
          --quick-open                   添加 RAR 快速打开信息
          --test                         创建后校验归档
          --comment <文件>               RAR/ZIP 注释文本文件
          --temp-dir <目录>              归档程序临时目录

        密码:
          --random-password, -r          按归档文件名生成兼容随机密码，默认开启
          --no-random-password           关闭随机密码派生
          --password, -p <密码>          直接提供自定义密码
          --password-file <文件>         从文件第一行读取密码
          --password-stdin               从标准输入第一行读取密码
          --password-name <archive|base> 密码依据：a.rar（默认）或 a

        处理策略:
          --existing <skip|update|overwrite>
                                        已有文件策略
          --lock                         锁定 RAR 归档（不能与 --existing update 同时使用）
          --skip-processed / --no-skip-processed
                                        启用或关闭跳过已处理项目
          --delete-source                成功后删除源文件
          --move-source                  成功后移动源文件
          --max-size, --max-size-gb <GB> 最大处理总量，0 表示不限
          --shutdown                     全部完成后请求关机
          --concurrency, -j <1..16>     并发执行任务数（默认 1 串行）
          --add-enclosures / --no-add-enclosures
                                        启用或关闭附件目录
          --enclosure-list <文件>        旧版兼容：换行分隔的附件目录
          --enclosure <目录...>          附件目录，可重复指定

        日志:
          --log-file <文件>              日志文件路径
          --log-retention-days <天数>    过期日志保留天数（默认 7，支持 --log-retention 别名）
          --verbose                      输出详细进度
          --quiet, -q                    仅向 stderr 输出错误
          --dry-run                      列出将处理的项目，不创建目录或归档
          --help, -h, -?, /?             显示帮助
          --version                      显示版本

        语言与输出:
          --language, --lang <zh-CN|zh-TW|en|ja|de>
                                        界面与服务层消息语言（接受 en-US、ja-JP、de-DE
                                        等区域变体，自动归一），默认 zh-CN；单独使用
                                        且不带模式动词时直接以该语言启动图形界面
          --output-format <text|json>   stdout 输出格式，默认 text；json 模式按
                                        NDJSON 逐行输出 progress/item/summary 事件，
                                        命令与进程原始输出（含密码）不进入 JSON 流，
                                        人类可读提示改写入 stderr

        示例:
          BatchCompress.Avalonia compress -i ./data -o ./out -e 7z --password-file ./password.txt
          BatchCompress.Avalonia extract -i ./archive.7z -o ./out --password-stdin
        """;

    // GLM-5.3, 2026-11-04：批次 4——zh-TW 標準正體中文幫助（術語與 zh-CN 版逐節對應）。
    private const string TraditionalChineseHelpText =
        """
        批次壓縮解壓工具

        用法:
          BatchCompress.Avalonia [gui]
          BatchCompress.Avalonia compress -i <路徑...> -o <輸出目錄> [選項]
          BatchCompress.Avalonia extract -i <封存...> -o <輸出目錄> [選項]

        模式:
          compress, --compress, -c       執行無介面批次壓縮
          extract, decompress, --decompress, -d
                                        執行無介面批次解壓
          gui, --gui, -g                 明確啟動圖形介面

        來源與輸出:
          --source, -s <目錄>            批次處理來源目錄，壓縮時處理其直接子項
          --input, -i <路徑...>          明確輸入的檔案或目錄，可重複指定
          --output, -o <目錄>            輸出目錄
          --text-file, -t <檔案>         解壓檔名與逐項密碼 TXT 清單

        壓縮參數:
          --extension, --format, -e      建立：rar、7z、zip、tar、gz、bz2、xz、wim；解壓：7zz 可讀取格式
          --level, -l <0..5>             壓縮層級
          --solid / --no-solid           啟用或關閉固實壓縮
          --volume-size, -v <數字>       分卷數值
          --volume-unit <b|k|m|g|t>      分卷單位
          --recovery <0..100>            RAR 復原記錄百分比
          --quick-open                   加入 RAR 快速開啟資訊
          --test                         建立後校驗封存
          --comment <檔案>               RAR/ZIP 註解文字檔
          --temp-dir <目錄>              封存程式暫存目錄

        密碼:
          --random-password, -r          依封存檔名產生相容隨機密碼，預設開啟
          --no-random-password           關閉隨機密碼衍生
          --password, -p <密碼>          直接提供自訂密碼
          --password-file <檔案>         從檔案第一行讀取密碼
          --password-stdin               從標準輸入第一行讀取密碼
          --password-name <archive|base> 密碼依據：a.rar（預設）或 a

        處理策略:
          --existing <skip|update|overwrite>
                                        既有檔案策略
          --lock                         鎖定 RAR 封存（不能與 --existing update 同時使用）
          --skip-processed / --no-skip-processed
                                        啟用或關閉跳過已處理項目
          --delete-source                成功後刪除來源檔案
          --move-source                  成功後移動來源檔案
          --max-size, --max-size-gb <GB> 最大處理總量，0 表示不限
          --shutdown                     全部完成後請求關機
          --concurrency, -j <1..16>     並行執行任務數（預設 1 串列）
          --add-enclosures / --no-add-enclosures
                                        啟用或關閉附件目錄
          --enclosure-list <檔案>        舊版相容：換行分隔的附件目錄
          --enclosure <目錄...>          附件目錄，可重複指定

        日誌:
          --log-file <檔案>              日誌檔案路徑
          --log-retention-days <天數>    過期日誌保留天數（預設 7，支援 --log-retention 別名）
          --verbose                      輸出詳細進度
          --quiet, -q                    僅向 stderr 輸出錯誤
          --dry-run                      列出將處理的項目，不建立目錄或封存
          --help, -h, -?, /?             顯示說明
          --version                      顯示版本

        語言與輸出:
          --language, --lang <zh-CN|zh-TW|en|ja|de>
                                        介面與服務層訊息語言（接受 en-US、ja-JP、de-DE
                                        等區域變體，自動歸一），預設 zh-CN；單獨使用
                                        且不帶模式動詞時直接以該語言啟動圖形介面
          --output-format <text|json>   stdout 輸出格式，預設 text；json 模式按
                                        NDJSON 逐行輸出 progress/item/summary 事件，
                                        命令與程序原始輸出（含密碼）不進入 JSON 流，
                                        人類可讀提示改寫入 stderr

        範例:
          BatchCompress.Avalonia compress -i ./data -o ./out -e 7z --password-file ./password.txt
          BatchCompress.Avalonia extract -i ./archive.7z -o ./out --password-stdin
        """;

    // GLM-5.3, 2026-11-04：批次 3——英文帮助。--help 可与 --language/--lang 组合输出对应语言。
    private const string EnglishHelpText =
        """
        Batch Compress & Extract Tool

        Usage:
          BatchCompress.Avalonia [gui]
          BatchCompress.Avalonia compress -i <paths...> -o <output dir> [options]
          BatchCompress.Avalonia extract -i <archives...> -o <output dir> [options]

        Modes:
          compress, --compress, -c       Run headless batch compression
          extract, decompress, --decompress, -d
                                        Run headless batch extraction
          gui, --gui, -g                 Launch the graphical interface explicitly

        Sources and output:
          --source, -s <dir>             Batch source directory; compression processes
                                        its direct children
          --input, -i <paths...>        Explicit files or directories, repeatable
          --output, -o <dir>             Output directory
          --text-file, -t <file>        Extraction TXT list of names and per-item passwords

        Compression:
          --extension, --format, -e <format>
                                        Create: rar, 7z, zip, tar, gz, bz2, xz, wim;
                                        extract: formats readable by 7zz
          --level, -l <0..5>            Compression level
          --solid / --no-solid          Enable or disable solid archiving
          --volume-size, -v <number>    Split volume size
          --volume-unit <b|k|m|g|t>     Split volume unit
          --recovery <0..100>           RAR recovery record percent
          --quick-open                   Add RAR quick open information
          --test                         Verify archives after creation
          --comment <file>               RAR/ZIP comment text file
          --temp-dir <dir>               Temporary directory for archiver

        Passwords:
          --random-password, -r          Derive legacy random password from archive
                                        name (default on)
          --no-random-password           Disable random password derivation
          --password, -p <password>      Provide a custom password directly
          --password-file <file>         Read the password from the first line
          --password-stdin               Read the password from standard input
          --password-name <archive|base> Derive from a.rar (default) or a

        Processing:
          --existing <skip|update|overwrite>
                                        Existing file policy
          --lock                         Lock RAR archives (conflicts with --existing update)
          --skip-processed / --no-skip-processed
                                        Enable or disable skipping processed items
          --delete-source                Delete sources after success
          --move-source                  Move sources after success
          --max-size, --max-size-gb <GB> Total size cap, 0 means unlimited
          --shutdown                     Request shutdown after completion
          --concurrency, -j <1..16>     Concurrent tasks to run (default 1, serial)
          --add-enclosures / --no-add-enclosures
                                        Enable or disable enclosure directories
          --enclosure-list <file>        Legacy: newline separated enclosure list
          --enclosure <dir...>           Enclosure directories, repeatable

        Logging:
          --log-file <file>              Log file path
          --log-retention-days <days>    Days to retain expired log files (default 7, --log-retention supported)
          --verbose                      Print detailed progress
          --quiet, -q                    Only print errors to stderr
          --dry-run                      List items without creating anything
          --help, -h, -?, /?             Show help
          --version                      Show version

        Language and output:
          --language, --lang <zh-CN|zh-TW|en|ja|de>
                                        Language for UI and service messages
                                        (regional variants such as en-US, ja-JP and
                                        de-DE are normalized), default zh-CN; used
                                        alone without a mode verb it launches the
                                        GUI directly in that language
          --output-format <text|json>    stdout format, default text; json emits
                                        NDJSON progress/item/summary events per line,
                                        raw command/process output (with passwords)
                                        never enters the JSON stream and human
                                        readable hints go to stderr

        Examples:
          BatchCompress.Avalonia compress -i ./data -o ./out -e 7z --password-file ./password.txt
          BatchCompress.Avalonia extract -i ./archive.7z -o ./out --password-stdin
        """;

    // Antigravity, 2026-11-07：批次 14——日文帮助（与 LocalizationService 术语一致）。
    private const string JapaneseHelpText =
        """
        バッチ圧縮解凍ツール

        使用法:
          BatchCompress.Avalonia [gui]
          BatchCompress.Avalonia compress -i <パス...> -o <出力先ディレクトリ> [オプション]
          BatchCompress.Avalonia extract -i <アーカイブ...> -o <出力先ディレクトリ> [オプション]

        モード:
          compress, --compress, -c       ヘッドレスバッチ圧縮を実行
          extract, decompress, --decompress, -d
                                        ヘッドレスバッチ解凍を実行
          gui, --gui, -g                 グラフィカルインターフェースを明示的に起動

        ソースと出力:
          --source, -s <ディレクトリ>    バッチ処理ソースディレクトリ。圧縮時は直下の子項目を処理
          --input, -i <パス...>          明示的に入力するファイルまたはディレクトリ（複数指定可）
          --output, -o <ディレクトリ>    出力先ディレクトリ
          --text-file, -t <ファイル>     解凍ファイル名と個別パスワードの TXT リスト

        圧縮パラメータ:
          --extension, --format, -e <形式>
                                        作成：rar、7z、zip、tar、gz、bz2、xz、wim；解凍：7zz で読み取り可能な形式
          --level, -l <0..5>            圧縮率
          --solid / --no-solid          ソリッド圧縮の有効化または無効化
          --volume-size, -v <数値>      分割サイズ
          --volume-unit <b|k|m|g|t>     分割単位
          --recovery <0..100>           RAR リカバリレコード割合（%）
          --quick-open                  RAR クイックオープン情報を追加
          --test                        作成後にアーカイブを検証
          --comment <ファイル>          RAR/ZIP コメントテキストファイル
          --temp-dir <ディレクトリ>     アーカイブプログラムの一時ディレクトリ

        パスワード:
          --random-password, -r         アーカイブ名から互換ランダムパスワードを生成（デフォルト有効）
          --no-random-password          ランダムパスワード派生を無効化
          --password, -p <パスワード>   カスタムパスワードを直接指定
          --password-file <ファイル>    ファイルの先頭行からパスワードを読み込む
          --password-stdin              標準入力の先頭行からパスワードを読み込む
          --password-name <archive|base>
                                        パスワード基準：a.rar（デフォルト）または a

        処理ポリシー:
          --existing <skip|update|overwrite>
                                        既存ファイル処理ポリシー
          --lock                        RAR アーカイブをロック（--existing update との併用不可）
          --skip-processed / --no-skip-processed
                                        処理済み項目のスキップを有効化または無効化
          --delete-source               成功後にソースファイルを削除
          --move-source                 成功後にソースファイルを移動
          --max-size, --max-size-gb <GB>
                                        最大処理合計サイズ（0 は無制限）
          --shutdown                    すべての処理完了後にシャットダウンを要求
          --concurrency, -j <1..16>     並行タスク実行数（デフォルト 1、順次実行）
          --add-enclosures / --no-add-enclosures
                                        添付ディレクトリの有効化または無効化
          --enclosure-list <ファイル>   下位互換：改行区切りの添付ディレクトリ一覧
          --enclosure <ディレクトリ...> 添付ディレクトリ（複数指定可）

        ログ:
          --log-file <ファイル>         ログファイルのパス
          --log-retention-days <日数>   期限切れログの保持日数（デフォルト 7、--log-retention エイリアス対応）
          --verbose                     詳細な進捗を出力
          --quiet, -q                   エラーのみ stderr に出力
          --dry-run                     処理対象項目を一覧表示し、ディレクトリやアーカイブを作成しない
          --help, -h, -?, /?            ヘルプを表示
          --version                     バージョンを表示

        言語と出力:
          --language, --lang <zh-CN|zh-TW|en|ja|de>
                                        UI およびサービス層のメッセージ言語（en-US、ja-JP、de-DE などの
                                        地域バリアントを受け入れ、自動正規化）、デフォルトは zh-CN。
                                        モード動詞なしで単独使用された場合、その言語で GUI を直接起動
          --output-format <text|json>   stdout の出力形式（デフォルト text）。json モードは NDJSON 形式で
                                        行ごとに progress/item/summary イベントを出力。コマンドやプロセスの
                                        生出力（パスワードを含む）は JSON ストリームに含まれず、可読ヒントは
                                        stderr に書き込まれる

        例:
          BatchCompress.Avalonia compress -i ./data -o ./out -e 7z --password-file ./password.txt
          BatchCompress.Avalonia extract -i ./archive.7z -o ./out --password-stdin
        """;

    // Antigravity, 2026-11-07：批次 14——德文帮助（零中文字符，与 LocalizationService 德语术语对齐）。
    private const string GermanHelpText =
        """
        Batch-Komprimierungs- und Entpackungstool

        Verwendung:
          BatchCompress.Avalonia [gui]
          BatchCompress.Avalonia compress -i <Pfade...> -o <Ausgabeverzeichnis> [Optionen]
          BatchCompress.Avalonia extract -i <Archive...> -o <Ausgabeverzeichnis> [Optionen]

        Modi:
          compress, --compress, -c       Headless-Batch-Komprimierung ausfuehren
          extract, decompress, --decompress, -d
                                        Headless-Batch-Entpackung ausfuehren
          gui, --gui, -g                 Grafische Benutzeroberflaeche explizit starten

        Quelle und Ausgabe:
          --source, -s <Verzeichnis>     Batch-Quellverzeichnis; komprimiert direkte Unterelemente
          --input, -i <Pfade...>         Explizite Eingabedateien oder -verzeichnisse, wiederholbar
          --output, -o <Verzeichnis>     Ausgabeverzeichnis
          --text-file, -t <Datei>        TXT-Liste fuer Dateinamen und Passwoerter zum Entpacken

        Komprimierung:
          --extension, --format, -e <Format>
                                        Erstellen: rar, 7z, zip, tar, gz, bz2, xz, wim;
                                        Entpacken: von 7zz lesbare Formate
          --level, -l <0..5>            Komprimierungsstufe
          --solid / --no-solid          Solid-Archivierung aktivieren oder deaktivieren
          --volume-size, -v <Zahl>      Teilvolumengroesse
          --volume-unit <b|k|m|g|t>     Teilvolumeneinheit
          --recovery <0..100>           RAR-Wiederherstellungsdaten in Prozent
          --quick-open                  RAR-Schnelloeffnungsinformationen hinzufuegen
          --test                        Archive nach Erstellung ueberpruefen
          --comment <Datei>             Kommentartextdatei fuer RAR/ZIP
          --temp-dir <Verzeichnis>      Temporaeres Verzeichnis fuer Archivierer

        Passwoerter:
          --random-password, -r         Kompatibles Zufallspasswort aus Archivname ableiten (Standard aktiv)
          --no-random-password          Ableitung von Zufallspasswoertern deaktivieren
          --password, -p <Passwort>     Benutzerdefiniertes Passwort direkt angeben
          --password-file <Datei>       Passwort aus der ersten Zeile der Datei lesen
          --password-stdin              Passwort aus Standard-Eingabe (erste Zeile) lesen
          --password-name <archive|base>
                                        Passwortbasis: a.rar (Standard) oder a

        Verarbeitungsrichtlinien:
          --existing <skip|update|overwrite>
                                        Strategie fuer vorhandene Dateien
          --lock                        RAR-Archiv sperren (nicht kompatibel mit --existing update)
          --skip-processed / --no-skip-processed
                                        Ueberspringen bereits verarbeiteter Elemente aktivieren/deaktivieren
          --delete-source               Quelldateien nach erfolgreichem Abschluss loeschen
          --move-source                 Quelldateien nach erfolgreichem Abschluss verschieben
          --max-size, --max-size-gb <GB>
                                        Gesamtobergroesse in GB (0 bedeutet unbegrenzt)
          --shutdown                    Nach Abschluss aller Aufgaben Herunterfahren anfordern
          --concurrency, -j <1..16>     Gleichzeitig auszufuehrende Aufgaben (Standard 1, seriell)
          --add-enclosures / --no-add-enclosures
                                        Anhangsverzeichnisse aktivieren oder deaktivieren
          --enclosure-list <Datei>      Abwaertskompatibel: Zeilengetrennte Anhangsverzeichnisliste
          --enclosure <Verzeichnis...>  Anhangsverzeichnis, wiederholbar

        Protokollierung:
          --log-file <Datei>            Pfad zur Protokolldatei
          --log-retention-days <Tage>   Aufbewahrungsdauer abgelaufener Protokolle (Standard 7, Alias --log-retention)
          --verbose                     Detaillierten Fortschritt ausgeben
          --quiet, -q                   Nur Fehler nach stderr ausgeben
          --dry-run                     Zu verarbeitende Elemente auflisten, keine Verzeichnisse/Archive anlegen
          --help, -h, -?, /?            Hilfe anzeigen
          --version                     Version anzeigen

        Sprache und Ausgabe:
          --language, --lang <zh-CN|zh-TW|en|ja|de>
                                        Sprache fuer UI- und Dienstnachrichten (regionale Varianten wie
                                        en-US, ja-JP und de-DE werden normalisiert), Standard zh-CN;
                                        allein ohne Modus verwendet, startet die GUI direkt in dieser Sprache
          --output-format <text|json>   stdout-Ausgabeformat, Standard text; json liefert NDJSON
                                        progress/item/summary Ereignisse pro Zeile, Rohausgaben mit Passwoertern
                                        gelangen nicht in den JSON-Stream, lesbare Hinweise gehen nach stderr

        Beispiele:
          BatchCompress.Avalonia compress -i ./data -o ./out -e 7z --password-file ./password.txt
          BatchCompress.Avalonia extract -i ./archive.7z -o ./out --password-stdin
        """;
}
