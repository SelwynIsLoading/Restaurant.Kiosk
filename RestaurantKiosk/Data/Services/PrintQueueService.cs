using System.Collections.Concurrent;
using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

/// <summary>
/// Service for managing print queue when Raspberry Pi polls for jobs
/// </summary>
public interface IPrintQueueService
{
    /// <summary>
    /// Add a print job to the queue
    /// </summary>
    Task<string> QueuePrintJobAsync(ReceiptData receiptData);
    
    /// <summary>
    /// Get the next pending print job
    /// </summary>
    Task<PrintJob?> GetNextPrintJobAsync();
    
    /// <summary>
    /// Mark a print job as completed
    /// </summary>
    Task MarkJobCompletedAsync(string jobId);
    
    /// <summary>
    /// Mark a print job as failed
    /// </summary>
    Task MarkJobFailedAsync(string jobId, string error);
    
    /// <summary>
    /// Get print job status
    /// </summary>
    Task<PrintJobStatus?> GetJobStatusAsync(string jobId);
}

public class PrintQueueService : IPrintQueueService
{
    private readonly ILogger<PrintQueueService> _logger;
    
    // In-memory queue (for production, use Redis or database)
    private static readonly ConcurrentQueue<PrintJob> _printQueue = new();
    private static readonly ConcurrentDictionary<string, PrintJob> _allJobs = new();

    public PrintQueueService(ILogger<PrintQueueService> logger)
    {
        _logger = logger;
    }

    public Task<string> QueuePrintJobAsync(ReceiptData receiptData)
    {
        var jobId = $"PRINT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        
        var printJob = new PrintJob
        {
            JobId = jobId,
            Receipt = receiptData,
            QueuedAt = DateTime.UtcNow,
            Status = PrintJobStatus.Pending
        };
        
        _printQueue.Enqueue(printJob);
        _allJobs[jobId] = printJob;
        
        _logger.LogInformation("Queued print job {JobId} for order {OrderNumber}", 
            jobId, receiptData.OrderNumber);
        
        return Task.FromResult(jobId);
    }

    public Task<PrintJob?> GetNextPrintJobAsync()
    {
        if (_printQueue.TryDequeue(out var job))
        {
            job.Status = PrintJobStatus.Printing;
            job.PrintStartedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Dequeued print job {JobId} for order {OrderNumber}", 
                job.JobId, job.Receipt.OrderNumber);
            
            return Task.FromResult<PrintJob?>(job);
        }
        
        return Task.FromResult<PrintJob?>(null);
    }

    public Task MarkJobCompletedAsync(string jobId)
    {
        if (_allJobs.TryGetValue(jobId, out var job))
        {
            job.Status = PrintJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Print job {JobId} completed for order {OrderNumber}", 
                jobId, job.Receipt.OrderNumber);
            
            // Clean up old jobs after 5 minutes
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                _allJobs.TryRemove(jobId, out _);
            });
        }
        
        return Task.CompletedTask;
    }

    public Task MarkJobFailedAsync(string jobId, string error)
    {
        if (_allJobs.TryGetValue(jobId, out var job))
        {
            job.Status = PrintJobStatus.Failed;
            job.Error = error;
            job.CompletedAt = DateTime.UtcNow;
            
            _logger.LogWarning("Print job {JobId} failed for order {OrderNumber}: {Error}", 
                jobId, job.Receipt.OrderNumber, error);
        }
        
        return Task.CompletedTask;
    }

    public Task<PrintJobStatus?> GetJobStatusAsync(string jobId)
    {
        if (_allJobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult<PrintJobStatus?>(job.Status);
        }
        
        return Task.FromResult<PrintJobStatus?>(null);
    }
}

public class PrintJob
{
    public string JobId { get; set; } = string.Empty;
    public ReceiptData Receipt { get; set; } = new();
    public PrintJobStatus Status { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? PrintStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
}

public enum PrintJobStatus
{
    Pending,
    Printing,
    Completed,
    Failed
}

