using Ecommerce.Gateway.YARP.Context;
using Ecommerce.Gateway.YARP.Dtos;
using Ecommerce.Gateway.YARP.Models;
using Ecommerce.Gateway.YARP.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TS.Result;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();

builder.Services.AddDbContext<ApplicationDbContext>(opt =>
{
    opt.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSql"));
});

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddAuthentication().AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration.GetSection("JWT:Issuer").Value,
        ValidAudience =builder.Configuration.GetSection("JWT:Audience").Value,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration.GetSection("JWT:SecretKey").Value ?? "")),
        ValidateLifetime = true,

    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors(x=>x.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());

app.MapGet("/", () => "Hello World!");

app.MapPost("/auth/register", async (RegisterDto request, ApplicationDbContext context,CancellationToken cancellationToken) =>
{
    bool isUserNameExists = await context.Users.AnyAsync(p=>p.Name ==request.UserName,cancellationToken);

    if (isUserNameExists)
    {
        return Results.BadRequest(Result<string>.Failure("User has already created"));
    }

    User user = new()
    {
        Name = request.UserName,
        Password = request.Password,
    };

    await context.AddAsync(user,cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Results.Ok(Result<string>.Succeed("User has been created"));


});

app.MapPost("/auth/login", async (LoginDto request, ApplicationDbContext context, CancellationToken cancellationToken) =>
{
    User? user =await context.Users.FirstOrDefaultAsync(p=>p.Name == request.UserName,cancellationToken);

    if(user == null)
    {
        return Results.BadRequest(Result<string>.Failure("User has not been found"));
    };

    JwtProvider jwtProvider = new(builder.Configuration);

    //token create

    string token = jwtProvider.CreateToken(user);
    return Results.Ok(Result<string>.Succeed(token));
});

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

using(var scope = app.Services.CreateScope())
{
    var srv= scope.ServiceProvider;
    var context = srv.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
}

app.Run();
