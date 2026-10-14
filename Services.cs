using System.IdentityModel.Tokens.Jwt; using System.Security.Claims; using System.Text; using System.Text.Json; using System.Collections.Concurrent; using Microsoft.SemanticKernel; using Microsoft.SemanticKernel.ChatCompletion; using Microsoft.SemanticKernel.Connectors.Google; using Microsoft.SemanticKernel.Connectors.OpenAI; using Microsoft.Extensions.DependencyInjection; using AISmartLearning.API.Data; using AISmartLearning.API.Models; using Microsoft.AspNetCore.Identity; using Microsoft.EntityFrameworkCore; using Microsoft.IdentityModel.Tokens; 
namespace AISmartLearning.API.Services;
public sealed class JwtTokenService(IConfiguration config) { public (string Token,DateTime Expires) Create(User u) { var exp=DateTime.UtcNow.AddHours(4);var claims=new[]{new Claim(ClaimTypes.NameIdentifier,u.UserId.ToString()),new Claim(ClaimTypes.Name,u.FullName),new Claim(ClaimTypes.Email,u.Email),new Claim(ClaimTypes.Role,u.Role?.RoleName??"Student")};var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!));var token=new JwtSecurityToken(config["Jwt:Issuer"],config["Jwt:Audience"],claims,expires:exp,signingCredentials:new SigningCredentials(key,SecurityAlgorithms.HmacSha256));return(new JwtSecurityTokenHandler().WriteToken(token),exp); } }
public sealed class InteractionLogService(AppDbContext db) { public static int UserId(ClaimsPrincipal p)=>int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0; public async Task Write(int uid,string action,string details,int? cid=null,CancellationToken ct=default) { db.InteractionLogs.Add(new InteractionLog{UserId=uid,ConversationId=cid,Action=action,Details=details.Length>1900?details[..1900]:details,CreatedAt=DateTime.UtcNow});await db.SaveChangesAsync(ct); } }
public sealed class AiService(IConfiguration config, ILogger<AiService> logger)
{
    // Reuse provider clients/kernels between chat requests instead of rebuilding them for every message.
    private static readonly ConcurrentDictionary<string, Kernel> Kernels = new(StringComparer.Ordinal);
    public async Task<string> Reply(string role, string prompt, IReadOnlyList<(string Sender, string Text)> history, CancellationToken ct)
    {
        var key = config["AI:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("AI is not configured. Set AI:ApiKey using Visual Studio > Manage User Secrets.");

        var provider = (config["AI:Provider"] ?? "Gemini").Trim();
        var model = config["AI:Model"] ?? (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) ? "gpt-4o-mini" : "gemini-3-flash-preview");
        var cacheKey = $"{provider}|{model}|{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key)))}";
        var kernel = Kernels.GetOrAdd(cacheKey, _ =>
        {
            var kernelBuilder = Kernel.CreateBuilder();
            if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) || provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
#pragma warning disable SKEXP0070
                kernelBuilder.AddGoogleAIGeminiChatCompletion(modelId: model, apiKey: key);
#pragma warning restore SKEXP0070
            }
            else if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                kernelBuilder.AddOpenAIChatCompletion(modelId: model, apiKey: key);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported AI:Provider '{provider}'. Use Gemini or OpenAI.");
            }
            return kernelBuilder.Build();
        });
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory(role == "Teacher"
            ? "You are a concise teaching assistant. Answer directly, explain accurately, and use examples only when useful."
            : "You are a concise, patient AI tutor. Answer directly in clear language, encourage learning, and do not claim to be human. Prefer short answers unless the user asks for detail.");
        // Shorter history reduces prompt size and time-to-first-response while preserving context.
        foreach (var item in history.TakeLast(6))
        {
            if (string.IsNullOrWhiteSpace(item.Text)) continue;
            if (item.Sender.Equals("AI", StringComparison.OrdinalIgnoreCase) || item.Sender.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                chatHistory.AddAssistantMessage(item.Text);
            else
                chatHistory.AddUserMessage(item.Text);
        }
        chatHistory.AddUserMessage(prompt);

        try
        {
            var results = await chat.GetChatMessageContentsAsync(chatHistory, cancellationToken: ct);
            var answer = string.Join("\n", results.Select(x => x.Content).Where(x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrWhiteSpace(answer))
                throw new InvalidOperationException("The AI provider returned an empty response. Check model access and provider safety settings.");
            return answer.Trim();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Semantic Kernel request failed for provider {Provider} and model {Model}", provider, model);
            var detail = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            throw new HttpRequestException($"{provider} AI request failed for model '{model}': {detail}", ex);
        }
    }
}
public static class DemoDataSeeder { public static async Task SeedAsync(IServiceProvider sp) { var db=sp.GetRequiredService<AppDbContext>();var hasher=sp.GetRequiredService<IPasswordHasher<User>>();foreach(var n in new[]{"Admin","Teacher","Student"})if(!await db.Roles.AnyAsync(x=>x.RoleName==n))db.Roles.Add(new Role{RoleName=n});await db.SaveChangesAsync();var demos=new[]{("Assessment Admin","admin@aismartlearning.local","Admin123!ChangeMe","Admin"),("Demo Teacher","teacher@aismartlearning.local","Teacher123!ChangeMe","Teacher"),("Demo Student","student@aismartlearning.local","Student123!ChangeMe","Student")};foreach(var d in demos){if(await db.Users.AnyAsync(x=>x.Email==d.Item2))continue;var r=await db.Roles.SingleAsync(x=>x.RoleName==d.Item4);var u=new User{FullName=d.Item1,Email=d.Item2,RoleId=r.RoleId,Role=r,IsActive=true};u.PasswordHash=hasher.HashPassword(u,d.Item3);db.Users.Add(u);}await db.SaveChangesAsync();} }