using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;

namespace ThunderbirdMsgFilterCopy.Resources;

/// <summary>
/// Hand-written accessor around the <c>Strings.resx</c> / <c>Strings.ja.resx</c> pair.
/// SPEC §8.3: English is the neutral culture (embedded in the main assembly); Japanese
/// ships as a <c>ja</c> satellite assembly. Any other culture falls back to English.
/// The actual culture is picked up from <see cref="CultureInfo.CurrentUICulture"/>.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new(
        "ThunderbirdMsgFilterCopy.Resources.Strings",
        typeof(Strings).GetTypeInfo().Assembly);

    private static string Get([CallerMemberName] string name = "")
        => Manager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    // Window / tabs
    public static string WindowTitle => Get();
    public static string TabExport => Get();
    public static string TabImport => Get();
    public static string ThunderbirdRunningBanner => Get();
    public static string NoProfileMessage => Get();

    // Status bar
    public static string StatusOsLabel => Get();
    public static string StatusProfileLabel => Get();

    // Menu
    public static string MenuFile => Get();
    public static string MenuFileExit => Get();
    public static string MenuTools => Get();
    public static string MenuToolsUndoLastImport => Get();
    public static string MenuHelp => Get();
    public static string MenuHelpAbout => Get();

    // Profile badges
    public static string ProfileBadgeInUse => Get();
    public static string ProfileBadgeEmpty => Get();
    public static string ProfileBadgeFilterCountFormat => Get();

    // Export tab
    public static string ExportHeader => Get();
    public static string ExportSelectAll => Get();
    public static string ExportDeselectAll => Get();
    public static string ExportExecute => Get();

    // Account list items
    public static string AccountTypeImap => Get();
    public static string AccountTypePop => Get();
    public static string AccountTypeLocal => Get();
    public static string AccountLocalDisplayName => Get();
    public static string ExportAccountDetailFormat => Get();

    // Import tab drop zone
    public static string ImportDropHint => Get();
    public static string ImportOrSeparator => Get();
    public static string ImportPickFile => Get();

    // Import preview
    public static string ImportPreviewSourceFormat => Get();
    public static string ImportPreviewExportedAtFormat => Get();
    public static string ImportPreviewSourceLabelFormat => Get();
    public static string ImportPreviewTargetEmpty => Get();
    public static string ImportPreviewStatusMatchedFormat => Get();
    public static string ImportPreviewStatusUnmatched => Get();
    public static string ImportPreviewVersionWarningFormat => Get();

    // Common buttons
    public static string CommonCancel => Get();
    public static string CommonExecute => Get();
    public static string CommonOk => Get();
    public static string CommonClose => Get();
    public static string CommonDetails => Get();

    // Export commands
    public static string ExportSaveDialogTitle => Get();
    public static string ExportCompleteTitle => Get();
    public static string ExportCompleteMessageFormat => Get();
    public static string ExportErrorNoWritePermission => Get();
    public static string ExportErrorGeneric => Get();

    // Import commands
    public static string ImportOpenDialogTitle => Get();
    public static string ImportErrorNoProfile => Get();
    public static string ImportErrorReadFailedTitle => Get();
    public static string ImportErrorReadFailedGeneric => Get();

    // Import execute confirm / result
    public static string ImportConfirmTitle => Get();
    public static string ImportConfirmMessage => Get();
    public static string ImportCompleteTitle => Get();
    public static string ImportCompleteMessageFormat => Get();
    public static string ImportErrorsHeader => Get();
    public static string ImportExecuteNotPossibleTitle => Get();
    public static string ImportExecuteErrorGeneric => Get();

    // Importer / package errors
    public static string ImportPackageErrorFileNotFound => Get();
    public static string ImportPackageErrorCorrupted => Get();
    public static string ImportPackageErrorUnsupportedSchema => Get();
    public static string ImportPackageErrorThunderbirdRunning => Get();
    public static string ImportPackageErrorMissingZipEntryFormat => Get();

    // Undo last import
    public static string UndoThunderbirdRunningTitle => Get();
    public static string UndoThunderbirdRunningMessage => Get();
    public static string UndoConfirmTitle => Get();
    public static string UndoConfirmMessageFormat => Get();
    public static string UndoConfirmOk => Get();
    public static string UndoCompleteTitle => Get();
    public static string UndoCompleteMessage => Get();
    public static string UndoErrorTitle => Get();

    // Main window errors / about
    public static string ProfileDetectError => Get();
    public static string AccountScanError => Get();
    public static string AboutVersionFormat => Get();
    public static string AboutWindowTitleFormat => Get();

    // Dialog
    public static string DialogErrorTitle => Get();
}
