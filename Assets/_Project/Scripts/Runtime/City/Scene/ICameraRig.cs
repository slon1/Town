namespace ZombieCity.City.Scene
{
    public interface ICameraRig
    {
        void ResetView();
        void SetViewFrame(float focusWorldX, float focusWorldZ, float orthographicSize);
        CameraRigSettings ActiveSettings { get; }
    }
}
