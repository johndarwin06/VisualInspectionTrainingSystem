namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents the current state of the quiz.
    /// </summary>
    public enum QuizState
    {
        Idle,

        Loading,

        Ready,

        Answered,

        Completed
    }
}