// Modified for PhoneControl on 2026-10-03 from scrcpy 4.1 (Apache-2.0).
// Allocate Android touch IDs cyclically instead of immediately reusing an UP ID.
package com.genymobile.scrcpy.control;

import com.genymobile.scrcpy.model.Point;

import android.view.MotionEvent;

import java.util.ArrayList;
import java.util.List;

public class PointersState {

    public static final int MAX_POINTERS = 10;

    private final List<Pointer> pointers = new ArrayList<>();
    private int nextLocalId;

    private int indexOf(long id) {
        for (int i = 0; i < pointers.size(); ++i) {
            Pointer pointer = pointers.get(i);
            if (pointer.getId() == id) {
                return i;
            }
        }
        return -1;
    }

    private boolean isLocalIdAvailable(int localId) {
        for (int i = 0; i < pointers.size(); ++i) {
            Pointer pointer = pointers.get(i);
            if (pointer.getLocalId() == localId) {
                return false;
            }
        }
        return true;
    }

    private int nextUnusedLocalId() {
        // Keep IDs inside the game's ten touch slots. Android accepts IDs up
        // to 31, but game input handlers may use the ID as a touch-slot index.
        // Rotate available slots to avoid immediate UP/DOWN reuse.
        for (int attempt = 0; attempt < MAX_POINTERS; ++attempt) {
            int localId = nextLocalId;
            nextLocalId = (nextLocalId + 1) % MAX_POINTERS;
            if (isLocalIdAvailable(localId)) {
                return localId;
            }
        }
        return -1;
    }

    public Pointer get(int index) {
        return pointers.get(index);
    }

    public int getPointerIndex(long id) {
        int index = indexOf(id);
        if (index != -1) {
            // already exists, return it
            return index;
        }
        if (pointers.size() >= MAX_POINTERS) {
            // it's full
            return -1;
        }
        // id 0 is reserved for mouse events
        int localId = nextUnusedLocalId();
        if (localId == -1) {
            throw new AssertionError("pointers.size() < maxFingers implies that a local id is available");
        }
        Pointer pointer = new Pointer(id, localId);
        pointers.add(pointer);
        return pointers.size() - 1;
    }

    /**
     * Initialize the motion event parameters.
     *
     * @param props  the pointer properties
     * @param coords the pointer coordinates
     * @return The number of items initialized (the number of pointers).
     */
    public int update(MotionEvent.PointerProperties[] props, MotionEvent.PointerCoords[] coords) {
        int count = pointers.size();
        for (int i = 0; i < count; ++i) {
            Pointer pointer = pointers.get(i);

            // id 0 is reserved for mouse events
            props[i].id = pointer.getLocalId();

            Point point = pointer.getPoint();
            coords[i].x = point.getX();
            coords[i].y = point.getY();
            coords[i].pressure = pointer.getPressure();
        }
        cleanUp();
        return count;
    }

    /**
     * Remove all pointers which are UP.
     */
    private void cleanUp() {
        for (int i = pointers.size() - 1; i >= 0; --i) {
            Pointer pointer = pointers.get(i);
            if (pointer.isUp()) {
                pointers.remove(i);
            }
        }
    }
}
