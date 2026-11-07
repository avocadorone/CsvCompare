using CsvCompare.Forms;
using System.Text;

namespace CsvCompare;

/// <summary>
/// アプリケーションのエントリーポイント。
/// 起動時の初期設定を行い、メインフォームを起動する。
/// </summary>
internal static class Program
{
    /// <summary>
    /// アプリケーションの開始処理。
    /// STAスレッドの設定、文字コードプロバイダーの登録、
    /// Windows Formsの初期化、メインフォームの起動を行う。
    /// </summary>
    [STAThread]
    private static void Main()
    {
        // Windows-31J（コードページ932）などの追加文字コードを利用可能にする
        // CsvReaderで使用するEncoding.GetEncoding(932)に必要
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Windows Formsのアプリケーション設定を初期化
        ApplicationConfiguration.Initialize();

        // メイン画面を起動し、アプリケーションのメッセージループを開始
        Application.Run(new MainForm());
    }
}