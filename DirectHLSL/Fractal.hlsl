cbuffer FractalParameters : register(b0)
{
    float2 JuliaConstant;
    float EscapeRadius;
    uint Power;

    uint MaxIterations;
    uint Function;
    uint ColoringMethod;
    uint UseSmooth;
    uint OrbitTrapType;

    float3 PaletteBase;
    float PalettePadding1;

    float3 PaletteAmplitude;
    float PalettePadding2;

    float3 PaletteFrequency;
    float PalettePadding3;

    float3 PalettePhase;
    float PalettePadding4;

    float2 ViewCenter;
    float ViewScale;
    float ViewPadding;

    float BufferPadding1;
    float BufferPadding2;
    float BufferPadding3;
};

RWTexture2D<float4> Output;

struct EscapeResult
{
    uint iterations;
    float2 finalZ;
};

struct TrapResult
{
    uint iterations;
    float trapDistance;
    float2 finalZ;
};

struct CosPaletteData
{
    float3 base; //базовый цвет 
    float3 amplitude; //контраст палитры(интенсивность цветов)
    float3 frequency; //частота цветового цикла(скорость изменения цвета по мере увеличения итераций) 
    float3 phase; //свдиг цветовых каналов 
};

class Maths
{
    float2 Multiply(float2 a, float2 b)
    {
        return float2(
            a.x * b.x - a.y * b.y,
            a.x * b.y + a.y * b.x);
    }
    
    float2 Power(float2 z, uint pow)
    {
        float2 result = float2(1, 0);
        for (uint i = 0; i < pow; i++)
        {
            result = Multiply(result, z);
        }
        return result;
    }

    float2 Sine(float2 z)
    {
        return float2(sin(z.x) * cosh(z.y), cos(z.x) * sinh(z.y));
    }

    float2 Cosine(float2 z)
    {
        return float2(cos(z.x) * cosh(z.y), -sin(z.x) * sinh(z.y));
    }

    float DistanceToOrbitTrap(float2 z, uint trapType)
    {
        switch (trapType)
        {
            case 0: return length(z);       //точка
            case 1: return abs(z.y);        //горизонтальная линия
            case 2: return abs(z.x);        //вертикальная линия
            case 3: return min(abs(z.x), abs(z.y));     //крест
            case 4: return abs(length(z) - 0.5);        //круг(радиус 0.5)
            case 5:                              //квадрат
            {
                float2 distanceToEdge = abs(z) - 0.5;
                float outsideDistance = length(max(distanceToEdge, 0.0));
                float insideDistance = min(max(distanceToEdge.x, distanceToEdge.y), 0.0);
                return abs(outsideDistance + insideDistance);
            }
            case 6: return abs(abs(z.x) + abs(z.y) - 0.5) * 0.70710678;// ромб 
            default: return abs(length(z) - 0.5);//круг если ниче не выбрали
        }
    }
    
    uint CalculateIterations(float2 z, float2 c, uint pow, uint function, uint maxIterations, float escapeRadius)
    {
        uint iterations = 0;
        if (function == 2 || function == 3)
        {
            while (iterations < maxIterations && dot(z, z) <= escapeRadius)
            {
                z = Sine(z) + c;
                iterations++;
            }
        }
        else if (function == 4 || function == 5)
        {
            while (iterations < maxIterations && dot(z, z) <= escapeRadius)
            {
                z = Cosine(z) + c;
                iterations++;
            }
        }
        else
        {
            while (iterations < maxIterations && dot(z, z) <= escapeRadius)
            {
                z = Power(z, pow) + c;
                iterations++;
            }
        }
        return iterations;
    }

    EscapeResult CalculateEscapeTime(float2 z, float2 c, uint pow, uint function, uint maxIterations, float escapeRadius)
    {
        EscapeResult result;
        uint iterations = 0;
        if (function == 2 || function == 3)
        {
            while (iterations < maxIterations && dot(z, z) <= escapeRadius)
            {
                z = Sine(z) + c;
                iterations++;
            }
        }
        else if (function == 4 || function == 5)
        {
            while (iterations < maxIterations && dot(z, z) <= escapeRadius)
            {
                z = Cosine(z) + c;
                iterations++;
            }
        }
        else
        {
            while (iterations < maxIterations && dot(z, z) <= escapeRadius)
            {
                z = Power(z, pow) + c;
                iterations++;
            }
        }

        result.iterations = iterations;
        result.finalZ = z;
        return result;
    }

    TrapResult CalculateOrbitTrap(float2 z, float2 c, uint pow, uint function, uint maxIterations, float escapeRadius, uint trapType)
    {
        TrapResult result;
        uint iterations = 0;
        float trapDistance = 1e20;

        if (function == 2 || function == 3)
        {
            while (iterations < maxIterations && dot(z, z) <= escapeRadius)
            {
                z = Sine(z) + c;
                iterations++;
                trapDistance = min(trapDistance, DistanceToOrbitTrap(z, trapType));
            }
        }
        else if (function == 4 || function == 5)
        {
            while (iterations < maxIterations && dot(z, z) <= escapeRadius)
            {
                z = Cosine(z) + c;
                iterations++;
                trapDistance = min(trapDistance, DistanceToOrbitTrap(z, trapType));
            }
        }
        else
        {
            while (iterations < maxIterations && dot(z, z) <= escapeRadius)
            {
                z = Power(z, pow) + c;
                iterations++;
                trapDistance = min(trapDistance, DistanceToOrbitTrap(z, trapType));
            }
        }

        result.iterations = iterations;
        result.trapDistance = trapDistance;
        result.finalZ = z;
        return result;
    }
};

class Coloring
{
    float4 CosPalette(float t, CosPaletteData data)
    {
        float3 color = data.base + data.amplitude * cos(6.28318530718 * (data.frequency * t + data.phase));

        return float4(color, 1.0);
    }
    
    float4 EscapeTimeAlgorithm(float value, uint iterations, uint maxIterations, CosPaletteData data)
    {
        if (iterations == maxIterations)
            return float4(0.0, 0.0, 0.0, 1.0);

        float t = value / (float)maxIterations;

        return CosPalette(t, data);
    }
    
    float4 MonochromeEscapeTimeAlgorithm(uint iterations, uint maxIterations)
    {
        if (iterations == maxIterations)
            return float4(0.0, 0.0, 0.0, 1.0);

        float t = (float) iterations / (float) maxIterations;

        return float4(t, t, t, 1.0);
    }

    float4 MetallicSurface(EscapeResult result, uint maxIterations, CosPaletteData data)
    {
        if (result.iterations == maxIterations)
            return float4(0.0, 0.0, 0.0, 1.0);//внутри фрактала

        float t = (float)result.iterations / max((float)maxIterations, 1.0);
        float magnitude = max(length(result.finalZ), 0.0001);
        float2 direction = result.finalZ / magnitude;//направление от центра 

        float3 normal = normalize(float3(direction * 0.45, 0.9)); //псевдонормаль для имитации освещения
        float3 lightDirection = normalize(float3(-0.45, 0.35, 0.82));//направление света
        float3 viewDirection = float3(0.0, 0.0, 1.0); //от поверхности к камере 
        float3 halfDirection = normalize(lightDirection + viewDirection);//блик

        float diffuse = 0.25 + 0.75 * saturate(dot(normal, lightDirection)); //диффузное освещение
        float specular = pow(saturate(dot(normal, halfDirection)), 72.0);//зеркальный блик
        float bands = 0.72 + 0.28 * (0.5 + 0.5 * cos(magnitude * 18.0 + t * 6.28318530718));//металлические полосы

        float3 metalTint = lerp(   //оттенок металла
            float3(0.58, 0.64, 0.72),
            saturate(data.base * 1.35),
            0.35);
        float3 color = metalTint * diffuse * bands + specular * float3(1.0, 0.98, 0.92);

        return float4(saturate(color), 1.0);
    }

    float4 OrbitTrapColor(TrapResult result, CosPaletteData data)
    {
        float intensity = exp(-result.trapDistance * 8.0); //расстояние == интенсивность цвета(8-резкость)
        return CosPalette(intensity, data);
    }
};

float4 RenderPixel(float2 c, uint pow, uint maxIterations, float escapeRadius, uint Function, uint coloringMethod, uint orbitTrapType, CosPaletteData data, float2 constant)
{
    Coloring coloring;
    Maths maths;
    bool isJulia = Function == 1 || Function == 3 || Function == 5;
    float2 initialZ = isJulia ? c : float2(0.0, 0.0);
    float2 iterationConstant = isJulia ? constant : c;

    if (coloringMethod == 2)
    {
        TrapResult result = maths.CalculateOrbitTrap(
            initialZ, iterationConstant, pow, Function, maxIterations, escapeRadius, orbitTrapType);
        return coloring.OrbitTrapColor(result, data);
    }

    if (coloringMethod == 1)
    {
        uint iterations = maths.CalculateIterations(
            initialZ, iterationConstant, pow, Function, maxIterations, escapeRadius);
        return coloring.MonochromeEscapeTimeAlgorithm(iterations, maxIterations);
    }

    if (coloringMethod == 3)
    {
        EscapeResult result = maths.CalculateEscapeTime(
            initialZ, iterationConstant, pow, Function, maxIterations, escapeRadius);
        return coloring.MetallicSurface(result, maxIterations, data);
    }

    EscapeResult result = maths.CalculateEscapeTime(
        initialZ, iterationConstant, pow, Function, maxIterations, escapeRadius);
    float value = (float)result.iterations;

    if (UseSmooth != 0 && result.iterations < maxIterations)
    {
        float magnitude = length(result.finalZ);
        value += 1.0 - log(log(magnitude)) / log((float)pow);
    }

    return coloring.EscapeTimeAlgorithm(value, result.iterations, maxIterations, data);

}

[numthreads(8, 8, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint width, height;
    
    Output.GetDimensions(width, height);
    
    if (id.x >= width || id.y >= height)
        return;

    
    float2 uv = (float2(id.xy) + 0.5) / float2(width, height);
    float2 centeredUV = uv - float2(0.5, 0.5);
    centeredUV.x *= (float)width / (float)height;
    centeredUV.y = -centeredUV.y;
    
    float2 spot = ViewCenter + centeredUV * ViewScale;
    float2 constant = JuliaConstant; // для жулиа
    uint maxIterations = MaxIterations;
    float escapeRadius = EscapeRadius;
    uint power = Power;
    CosPaletteData data;
    data.base = PaletteBase;
    data.amplitude = PaletteAmplitude;
    data.frequency = PaletteFrequency;
    data.phase = PalettePhase;
    
    float4 color = RenderPixel(spot, power, maxIterations, escapeRadius, Function, ColoringMethod, OrbitTrapType, data, constant);
    
    Output[id.xy] = color;
}

 