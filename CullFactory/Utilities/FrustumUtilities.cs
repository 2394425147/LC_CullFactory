using CullFactory.Data;
using DunGen;
using UnityEngine;

namespace CullFactory.Utilities;

public static class FrustumUtilities
{
    private static Plane _top;
    private static Plane _right;
    private static Plane _bottom;
    private static Plane _left;

    private static Vector3 _topRight;
    private static Vector3 _bottomRight;
    private static Vector3 _bottomLeft;
    private static Vector3 _topLeft;

    private static float _halfWidth;
    private static float _height;

    public static bool TryTrimFrustum(this Doorway door,
                                      Camera camera,
                                      Plane[] frustum,
                                      out FrustumAtDoor result)
    {
        _halfWidth = door.socket.Size.x / 2;
        _height = door.socket.Size.y;

        _bottomRight.x = _topRight.x = _halfWidth;
        _bottomLeft.x = _topLeft.x = -_halfWidth;
        _topRight.y = _topLeft.y = _height;

        var transformation = door.transform.localToWorldMatrix;

        result = new FrustumAtDoor(door, frustum);
        camera.transform.position.MakeFrustum(transformation * _topRight,
                                              transformation * _bottomRight,
                                              transformation * _bottomLeft,
                                              transformation * _topLeft);

        // TODO: Finish implementation here

        return false;
    }

    private static void MakeFrustum(this Vector3 origin,
                                       Vector3 topRight,
                                       Vector3 bottomRight,
                                       Vector3 bottomLeft,
                                       Vector3 topLeft)
    {
        _top = new Plane(origin, topLeft, topRight);
        _right = new Plane(origin, topRight, bottomRight);
        _bottom = new Plane(origin, bottomRight, bottomLeft);
        _left = new Plane(origin, bottomLeft, topLeft);
    }
}
