#pragma once

#if defined(_WIN32)
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