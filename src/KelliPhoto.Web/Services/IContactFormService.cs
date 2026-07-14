using KelliPhoto.Web.Models;

namespace KelliPhoto.Web.Services;

public interface IContactFormService
{
    Task<ContactFormSubmitResult> SubmitAsync(ContactFormModel model, string ipAddress);
}

public sealed record ContactFormSubmitResult(bool Success, string Message);
