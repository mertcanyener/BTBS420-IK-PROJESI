using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.CandidateDocuments;

public sealed class CandidateDocumentFormViewModel
{
    [Required(ErrorMessage = "Belge türü seçimi zorunludur.")]
    [Display(Name = "Belge Türü")]
    public string DocumentType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Dosya seçimi zorunludur.")]
    [Display(Name = "Dosya")]
    public IFormFile? File { get; set; }

    public IReadOnlyList<CandidateDocumentTypeOptionViewModel> DocumentTypeOptions { get; set; } = [];
}
