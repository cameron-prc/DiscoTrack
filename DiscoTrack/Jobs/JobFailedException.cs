namespace DiscoTrack.Jobs;

public class JobFailedException(string message) : Exception(message);
