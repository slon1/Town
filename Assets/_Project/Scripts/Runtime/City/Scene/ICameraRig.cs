namespace ZombieCity.City.Scene
{
    public interface ICameraRig
    {
        void ResetView();
        void SetViewFrame(float focusWorldX, float focusWorldZ, float orthographicSize, float referenceAspect);
        void LogCaptureState(string tag);
        CameraRigSettings ActiveSettings { get; }
    }
}
