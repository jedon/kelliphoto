namespace KelliPhoto.Web.Services;

public interface IRateLimitService
{
    /// <summary>
    /// Checks if the IP address has exceeded the rate limit for contact form submissions.
    /// </summary>
    /// <param name="ipAddress">The client IP address</param>
    /// <returns>True if rate limit exceeded, false otherwise</returns>
    bool IsRateLimited(string ipAddress);

    /// <summary>
    /// Records a contact form submission for rate limiting purposes.
    /// </summary>
    /// <param name="ipAddress">The client IP address</param>
    void RecordSubmission(string ipAddress);
}
