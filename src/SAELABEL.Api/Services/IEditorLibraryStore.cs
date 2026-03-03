using SAELABEL.Api.Contracts;

namespace SAELABEL.Api.Services;

public interface IEditorLibraryStore
{
    IReadOnlyList<EditorElementDto> GetElements();
    EditorElementDto UpsertElement(UpsertEditorElementRequest request);
    bool DeleteElement(string id);

    IReadOnlyList<EditorDocumentSummaryDto> GetDocuments();
    EditorDocumentDto? GetDocument(string id);
    EditorDocumentDto UpsertDocument(UpsertEditorDocumentRequest request);
    bool DeleteDocument(string id);
}

