package android.view;

// JVM-only field doubles for testing pointer allocation; never included in the server.
public final class MotionEvent {
    public static final class PointerProperties { public int id; }
    public static final class PointerCoords { public float x, y, pressure; }
}
