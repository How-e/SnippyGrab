using SnippyGrab.Core;
namespace SnippyGrab.Tests;
public sealed class OperationFailureTests
{
    [Fact] public void FeedbackDoesNotExposeExceptionPayload()
    {
        foreach (var error in new Exception[] { new IOException("private OCR text"), new UnauthorizedAccessException("private title"), new InvalidDataException("private path"), new Exception("private screenshot"), new DllNotFoundException("private DLL path") })
        { var failure = OperationFailure.From(error); Assert.DoesNotContain("private", failure.Message); Assert.NotEqual(FailureKind.Cancelled, failure.Kind); }
    }
    [Fact] public void CategorizesBoundariesAndCancellation()
    {
        Assert.Equal(FailureKind.Cancelled, OperationFailure.From(new OperationCanceledException()).Kind);
        Assert.Equal(FailureKind.Permission, OperationFailure.From(new AggregateException(new UnauthorizedAccessException())).Kind);
        Assert.Equal(FailureKind.Storage, OperationFailure.From(new IOException()).Kind);
        Assert.Equal(FailureKind.Clipboard, OperationFailure.From(new System.Runtime.InteropServices.ExternalException()).Kind);
    }
}
