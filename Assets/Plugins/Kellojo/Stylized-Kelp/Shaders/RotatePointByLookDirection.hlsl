void RotatePointByLookDirection_float(float3 position, float3 center, float3 lookDir, out float3 result)
{
    // Step 1: Normalize look direction (now it's "up")
    float3 up = normalize(lookDir);

    // Step 2: Choose a forward vector (assume Z-forward world)
    float3 forward = float3(0, 0, 1);
    if (abs(dot(up, forward)) > 0.999)
    {
        forward = float3(1, 0, 0); // Fallback forward
    }

    // Step 3: Build basis
    float3 right = normalize(cross(up, forward));
    float3 newForward = cross(right, up);

    // New basis: right (X), up (Y), forward (Z)
    float3x3 rotationMatrix = float3x3(right, up, newForward);

    // Step 4: Apply rotation around center
    float3 local = position - center;
    float3 rotated = mul(local, rotationMatrix);
    result = rotated + center;
}