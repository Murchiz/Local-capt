using System.Threading.Tasks;

namespace CaptionGenerator.Services;

public interface IVisionLanguageModelClient
{
    Task<string> GenerateCaptionAsync(byte[] imageData, string prompt);
}
