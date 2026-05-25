using KelliPhoto.Web.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace KelliPhoto.Web.Shared;

public partial class NavMenu
{
    [Inject]
    private IOptions<IdentitySettings> IdentityOptions { get; set; } = default!;

    private bool AllowRegistration => IdentityOptions.Value.AllowRegistration;
}
