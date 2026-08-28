#pragma once

#if defined(_MSC_VER)
    #define EXPORT __declspec(dllexport)
#else
    #define EXPORT
#endif

extern "C"
{
    EXPORT void InitTracker(float fx, float fy, float cx, float cy);

    EXPORT bool ProcessFrame(
        unsigned char* imageData, int width, int height, float markerLengthMeters, 
        float* outTvec, float* outRvec
    );
}