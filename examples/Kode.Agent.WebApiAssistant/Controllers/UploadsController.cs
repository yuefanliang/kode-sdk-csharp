using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Kode.Agent.WebApiAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public UploadsController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        // Create files directory in content root
        var uploadsPath = Path.Combine(_environment.ContentRootPath, "files");
        Directory.CreateDirectory(uploadsPath);

        // Unique filename: Timestamp + Random + OriginalExtension
        var ext = Path.GetExtension(file.FileName);
        // Ensure random string is filename safe
        var random = Path.GetRandomFileName().Replace(".", "").Substring(0, 8);
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var newFileName = $"{timestamp}_{random}{ext}";
        var filePath = Path.Combine(uploadsPath, newFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new
        {
            fileName = newFileName,
            originalName = file.FileName,
            path = filePath,
            url = $"/api/uploads/{newFileName}"
        });
    }

    [HttpGet("{fileName}")]
    public IActionResult GetFile(string fileName)
    {
        var uploadsPath = Path.Combine(_environment.ContentRootPath, "files");
        var filePath = Path.Combine(uploadsPath, fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(filePath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(filePath, contentType, fileName);
    }
}
