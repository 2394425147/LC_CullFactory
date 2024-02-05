using CullFactory.Data;
using DunGen;
using UnityEngine;

namespace CullFactory.Utilities;

public static class FrustumUtilities
{
    private const int Top = 0;
    private const int Right = 1;
    private const int Bottom = 2;
    private const int Left = 3;

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

        var doorTransform = door.transform;
        var transformation = doorTransform.localToWorldMatrix;

        result = new FrustumAtDoor(door);

        var invertHorizontal = Vector3.Dot(doorTransform.forward, camera.transform.forward) < 0;

        Vector3 worldTopRight, worldBottomRight, worldBottomLeft, worldTopLeft;

        if (invertHorizontal)
        {
            worldTopRight = transformation * _topLeft;
            worldBottomRight = transformation * _bottomLeft;
            worldBottomLeft = transformation * _bottomRight;
            worldTopLeft = transformation * _topRight;
        }
        else
        {
            worldTopRight = transformation * _topRight;
            worldBottomRight = transformation * _bottomRight;
            worldBottomLeft = transformation * _bottomLeft;
            worldTopLeft = transformation * _topLeft;
        }

        var cameraTransform = camera.transform;
        var cameraPosition = cameraTransform.position;

        if (frustum[Top].GetDistanceToPoint(_bottomRight) < 0 ||
            frustum[Left].GetDistanceToPoint(_bottomRight) < 0 ||
            frustum[Bottom].GetDistanceToPoint(_topLeft) < 0 ||
            frustum[Right].GetDistanceToPoint(_topLeft) < 0)
            return false;

        cameraPosition.MakeFrustum(worldTopRight,
                                   worldBottomRight,
                                   worldBottomLeft,
                                   worldTopLeft);

        var topDifference = frustum[Top].GetDistanceToPoint(_topRight);
        var rightDifference = frustum[Right].GetDistanceToPoint(_topRight);
        var bottomDifference = frustum[Bottom].GetDistanceToPoint(_bottomLeft);
        var leftDifference = frustum[Left].GetDistanceToPoint(_bottomLeft);

        result.frustum[Top] = topDifference < 0 ? frustum[Top] : _top;
        result.frustum[Right] = rightDifference < 0 ? frustum[Right] : _right;
        result.frustum[Bottom] = bottomDifference < 0 ? frustum[Bottom] : _bottom;
        result.frustum[Left] = leftDifference < 0 ? frustum[Left] : _left;

        return true;
    }

    private static void MakeFrustum(this Vector3 origin,
                                    Vector3 topRight,
                                    Vector3 bottomRight,
                                    Vector3 bottomLeft,
                                    Vector3 topLeft)
    {
        // All planes point towards the inside
        _top = new Plane(origin, topRight, topLeft);
        _right = new Plane(origin, bottomRight, topRight);
        _bottom = new Plane(origin, bottomLeft, bottomRight);
        _left = new Plane(origin, topLeft, bottomLeft);
    }
}
