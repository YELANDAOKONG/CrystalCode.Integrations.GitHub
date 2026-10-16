namespace CrystalCode.Integrations.GitHub.PullRequests;

/// <summary>The review action GitHub stores on a pull request.</summary>
public enum PullRequestReviewEvent
{
    Comment,
    Approve,
    RequestChanges
}
