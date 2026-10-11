import com.genymobile.scrcpy.control.Pointer;
import com.genymobile.scrcpy.control.PointersState;
import com.genymobile.scrcpy.model.Point;
import android.view.MotionEvent;
import java.util.HashSet;

public class PointerStateTest {
    public static void main(String[] args) {
        PointersState state = new PointersState();
        MotionEvent.PointerProperties[] props = new MotionEvent.PointerProperties[10];
        MotionEvent.PointerCoords[] coords = new MotionEvent.PointerCoords[10];
        for (int i = 0; i < 10; i++) { props[i] = new MotionEvent.PointerProperties(); coords[i] = new MotionEvent.PointerCoords(); }
        Pointer fire = add(state, 10), scope = add(state, 11), camera = add(state, 1);
        int fireId = fire.getLocalId(), scopeId = scope.getLocalId();
        for (int turn = 0; turn < 128; turn++) {
            int oldId = camera.getLocalId();
            camera.setUp(true); state.update(props, coords);
            camera = add(state, 1); state.update(props, coords);
            require(camera.getLocalId() != oldId, "Renewed camera must have a fresh Android ID");
            require(fire.getLocalId() == fireId && scope.getLocalId() == scopeId, "Held action IDs must remain stable");
            require(state.getPointerIndex(1) == 2, "Existing camera lookup must not allocate another finger");
            require(camera.getLocalId() >= 0 && camera.getLocalId() < 10, "Every renewed camera ID must fit the game's ten touch slots");
            // A new action must remain usable after sustained camera renewals,
            // not just the actions that were already held at the start.
            Pointer action = add(state, 20);
            require(action.getLocalId() >= 0 && action.getLocalId() < 10, "Actions pressed after sweeping must use a game-compatible ID");
            state.update(props, coords);
            require(action.getLocalId() != camera.getLocalId() && action.getLocalId() != fireId && action.getLocalId() != scopeId,
                "A new action must not reuse a held finger's slot");
            action.setUp(true); state.update(props, coords);
        }
        for (int i = 3; i < 10; i++) add(state, 100 + i);
        require(state.getPointerIndex(999) == -1, "Eleventh finger must be rejected");
        state.update(props, coords);
        HashSet<Integer> ids = new HashSet<>();
        for (int i = 0; i < 10; i++) require(ids.add(props[i].id), "Active Android IDs must be unique");
        for (int i = 0; i < 10; i++) state.get(i).setUp(true);
        state.update(props, coords);
        add(state, 1); require(state.update(props, coords) == 1, "All released slots must be reusable");
        System.out.println("PASS Android pointer allocation: 128 camera renewals and new action presses, IDs limited to game slots 0..9, held fire/scope, wraparound, uniqueness, cap and cleanup");
    }
    private static Pointer add(PointersState state, long id) {
        Pointer p = state.get(state.getPointerIndex(id)); p.setPoint(new Point(100, 200)); p.setPressure(1); return p;
    }
    private static void require(boolean ok, String description) { if (!ok) throw new AssertionError(description); }
}
