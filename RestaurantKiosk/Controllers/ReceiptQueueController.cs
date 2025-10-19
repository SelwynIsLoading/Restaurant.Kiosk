using Microsoft.AspNetCore.Mvc;
using RestaurantKiosk.Data.Services;

namespace RestaurantKiosk.Controllers;

/// <summary>
/// API controller for receipt print queue (polling mode)
/// Used by Raspberry Pi to poll for print jobs
/// </summary>
[ApiController]
[Route("api/receipt/queue")]
public class ReceiptQueueController : ControllerBase
{
    private readonly IPrintQueueService _printQueueService;
    private readonly ILogger<ReceiptQueueController> _logger;

    public ReceiptQueueController(
        IPrintQueueService printQueueService,
        ILogger<ReceiptQueueController> logger)
    {
        _printQueueService = printQueueService;
        _logger = logger;
    }

    /// <summary>
    /// Get next pending print job (called by Raspberry Pi)
    /// </summary>
    [HttpGet("next")]
    public async Task<IActionResult> GetNextPrintJob()
    {
        try
        {
            var printJob = await _printQueueService.GetNextPrintJobAsync();
            
            if (printJob == null)
            {
                // No jobs pending - return 204 No Content
                return NoContent();
            }
            
            return Ok(new
            {
                hasPrintJob = true,
                jobId = printJob.JobId,
                receipt = printJob.Receipt,
                queuedAt = printJob.QueuedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting next print job");
            return StatusCode(500, new { success = false, message = "Error retrieving print job" });
        }
    }

    /// <summary>
    /// Mark print job as completed (called by Raspberry Pi)
    /// </summary>
    [HttpPost("complete/{jobId}")]
    public async Task<IActionResult> MarkJobCompleted(string jobId)
    {
        try
        {
            _logger.LogInformation("Marking print job {JobId} as completed", jobId);
            
            await _printQueueService.MarkJobCompletedAsync(jobId);
            
            return Ok(new { success = true, message = "Job marked as completed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking job completed: {JobId}", jobId);
            return StatusCode(500, new { success = false, message = "Error updating job status" });
        }
    }

    /// <summary>
    /// Mark print job as failed (called by Raspberry Pi)
    /// </summary>
    [HttpPost("failed/{jobId}")]
    public async Task<IActionResult> MarkJobFailed(string jobId, [FromBody] FailJobRequest request)
    {
        try
        {
            _logger.LogWarning("Marking print job {JobId} as failed: {Error}", jobId, request.Error);
            
            await _printQueueService.MarkJobFailedAsync(jobId, request.Error ?? "Unknown error");
            
            return Ok(new { success = true, message = "Job marked as failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking job failed: {JobId}", jobId);
            return StatusCode(500, new { success = false, message = "Error updating job status" });
        }
    }

    /// <summary>
    /// Get print job status
    /// </summary>
    [HttpGet("status/{jobId}")]
    public async Task<IActionResult> GetJobStatus(string jobId)
    {
        try
        {
            var status = await _printQueueService.GetJobStatusAsync(jobId);
            
            if (status == null)
            {
                return NotFound(new { success = false, message = "Job not found" });
            }
            
            return Ok(new
            {
                success = true,
                jobId = jobId,
                status = status.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job status: {JobId}", jobId);
            return StatusCode(500, new { success = false, message = "Error retrieving job status" });
        }
    }
}

public class FailJobRequest
{
    public string? Error { get; set; }
}

