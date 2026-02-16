void CalcLeafVertexPosition_float(
    float3 currentPos,
    float3 previousPos,
    float3 rootUp,
    out float3 leftPos,
    out float3 rightPos
) {
    float3 forward = normalize(currentPos - previousPos);
    
    float3 up = normalize(rootUp);
    if (abs(dot(forward, up)) > 0.999)
        up = float3(0, 1, 0);
    
    float3 right = normalize(cross(up, forward));
    float3 newUp = cross(forward, right);
    float3x3 rotMatrix = float3x3(right, newUp, forward);
    
    leftPos = currentPos + mul(rotMatrix, float3(-0.5, 0, 0));
    rightPos = currentPos + mul(rotMatrix, float3( 0.5, 0, 0));
}
