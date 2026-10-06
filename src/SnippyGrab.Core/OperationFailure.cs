using System.Runtime.InteropServices;
namespace SnippyGrab.Core;

public enum FailureKind { Cancelled, Permission, Storage, InvalidInput, Clipboard, Ocr, Settings, Unexpected }
public sealed record OperationFailure(FailureKind Kind, string Message)
{
    public static OperationFailure From(Exception error)
    {
        if (error is AggregateException aggregate) error = aggregate.Flatten().InnerExceptions[0];
        var kind = error switch
        {
            OperationCanceledException => FailureKind.Cancelled,
            UnauthorizedAccessException or System.Security.SecurityException => FailureKind.Permission,
            DllNotFoundException or TypeInitializationException => FailureKind.Ocr,
            InvalidDataException or ArgumentException or FormatException => FailureKind.InvalidInput,
            IOException => FailureKind.Storage,
            ExternalException => FailureKind.Clipboard,
            _ => FailureKind.Unexpected
        };
        return new(kind, kind switch
        {
            FailureKind.Cancelled => "Operation cancelled.",
            FailureKind.Permission => "Access was denied. Check folder permissions or Windows startup access, then retry.",
            FailureKind.Storage => "Storage could not be updated. Check free space and file locks, then retry. Existing captures remain available.",
            FailureKind.InvalidInput => "This input is invalid or unsupported. Review the image, path or settings and retry.",
            FailureKind.Clipboard => "Clipboard is unavailable. Close the app holding it and retry Copy.",
            FailureKind.Ocr => "Local OCR could not load. Use the complete package and install the Microsoft Visual C++ 2015–2022 x64 runtime.",
            _ => "This operation failed. Retry the action; if it repeats, restart SnippyGrab. Existing capture files are retained."
        });
    }
}
