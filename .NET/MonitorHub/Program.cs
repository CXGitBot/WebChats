using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MonitorHub.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Text;
using M = MonitorHub.Hubs.MonitorHub;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TokenOptions>(builder.Configuration.GetSection("Authentication"));
var token = builder.Configuration.GetSection("Authentication").Get<TokenOptions>();

// Add services to the container.
builder.Services.AddRazorPages();
//���ʵʱӦ��
builder.Services.AddSignalR(o=> { 
    o.MaximumReceiveMessageSize = 2097152; 
});
//�����֤
builder.Services.AddAuthorization();
//�����Ȩ
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
{
    o.RequireHttpsMetadata = false;
    o.SaveToken = true;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(token.Secret)),
        ValidIssuer = token.Issuer,

        ValidAudience = token.Audience,
        ValidateIssuer = false,
        ValidateAudience = false
    };

    //�����֤ʧ���¼�
    o.Events = new JwtBearerEvents()
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.HttpContext.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!(string.IsNullOrWhiteSpace(accessToken))
                && path.StartsWithSegments("/Monitor"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Add("Token-Expired", "true");
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.None;
});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SignalRServer", Version = "v1" });
    c.DocInclusionPredicate((docName, description) => true);
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT��Ȩ(���ݽ�������ͷ�н��д���) ���·�����Bearer {token} ���ɣ�ע������֮���пո�",
        Name = "Authorization",//jwtĬ�ϵĲ�������
        In = ParameterLocation.Header,//jwtĬ�ϴ��Authorization��Ϣ��λ��(����ͷ��)
        Type = SecuritySchemeType.ApiKey
    });
    //��֤��ʽ���˷�ʽΪȫ�����
    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement 
        {
            { 
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference()
                    {
                        Id = "Bearer",
                        Type = ReferenceType.SecurityScheme
                    }
                }, Array.Empty<string>() 
            }
        });
});
builder.Services.AddDistributedMemoryCache();//�ֲ�ʽ����
builder.Services.AddControllersWithViews();


var app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
//����Swagger
app.UseSwagger();
//����Swagger��ͼ
app.UseSwaggerUI(options =>
{
    options.ShowExtensions();
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "WebAPI V1");
    options.DocExpansion(DocExpansion.None);
    options.DocumentTitle = "MonitorHub";
    options.HeadContent = "<link rel=\"icon\" type=\"image/png\" href=\"/rowss.png\" />\n" +
    "<link rel=\"shortcut icon\" type=\"image/png\" href=\"/rowss.png\" />\n";
});
//����Http�ض���
app.UseHttpsRedirection();
//���þ�̬�ļ�
app.UseStaticFiles();
//������Ȩ
app.UseAuthentication();
//����·��
app.UseRouting();

//������֤
app.UseAuthorization();
//�����ս��
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Document}/{action=Swagger}/{id?}");
});
//����Razor��ͼ
app.MapRazorPages();
//��������
app.MapHub<M>("/Monitor", (o) => {

});
//����
app.Run();