using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

using PhotoMapper.Data;
using PhotoMapper.Web.Components;
using PhotoMapper.Web.Components.Account;
using PhotoMapper.Web.Email;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Accounts: ASP.NET Core Identity with the template's account pages (Components/Account).
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// The schema is created and updated by PhotoMapper.MigrationService, which finishes before this app starts.
builder.AddNpgsqlDbContext<ApplicationDbContext>("photomapperdb", settings =>
    settings.ConnectionString = DatabaseConnection.WithoutGssEncryption(settings.ConnectionString));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.User.RequireUniqueEmail = true;
        IdentityStoreSettings.Apply(options.Stores);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Account emails go out over SMTP: Mailpit locally, a real provider when deployed (the "mail" connection string).
string mailConnectionString = builder.Configuration.GetConnectionString("mail")
    ?? throw new InvalidOperationException("Connection string 'mail' is missing; the AppHost provides it.");
builder.Services.AddSingleton(SmtpSettings.Parse(mailConnectionString, allowUnencrypted: builder.Environment.IsDevelopment()));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, SmtpEmailSender>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Endpoints the account pages post to (logout, passkeys, external logins, personal data download).
app.MapAdditionalIdentityEndpoints();

app.MapDefaultEndpoints();

app.Run();
