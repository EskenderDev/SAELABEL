using SAE.STUDIO.Api.Contracts;

namespace SAE.STUDIO.Api.Services;

public interface IEditorLibraryStore
{
    IReadOnlyList<EditorElementDto> GetElements();
    EditorElementDto UpsertElement(UpsertEditorElementRequest request);
    bool DeleteElement(string id);

    IReadOnlyList<EditorDocumentSummaryDto> GetDocuments();
    EditorDocumentDto? GetDocument(string id);
    EditorDocumentDto? GetDocumentByName(string name);
    EditorDocumentDto UpsertDocument(UpsertEditorDocumentRequest request);
    bool DeleteDocument(string id);

    IReadOnlyList<EditorTemplateDto> GetTemplates();
    EditorTemplateDto UpsertTemplate(UpsertEditorTemplateRequest request);

    string? GetSetting(string key);
    void SaveSetting(string key, string value);
}

