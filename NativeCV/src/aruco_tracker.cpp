#include "aruco_tracker.hpp"
#include <opencv2/imgproc.hpp>

#if defined(__ANDROID__)
    #include <android/log.h>
    #include <opencv2/calib3d.hpp>
    #include <opencv2/objdetect.hpp>
    #define LOGI(...) __android_log_print(ANDROID_LOG_INFO, "NativeCV", __VA_ARGS__)
    #define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, "NativeCV", __VA_ARGS__)
#else
    #include <iostream>
    #include <opencv2/aruco.hpp>
    #define LOGI(...) printf(__VA_ARGS__); printf("\n")
    #define LOGE(...) printf(__VA_ARGS__); printf("\n")
#endif

namespace
{
    cv::Mat cameraMatrix;
    cv::Mat distCoeffs;

    #if defined(__ANDROID__)
        cv::Ptr<cv::aruco::ArucoDetector> arucoDetector;
    #else
        cv::Ptr<cv::aruco::Dictionary> dictionary;
        cv::Ptr<cv::aruco::DetectorParameters> detectorParams;
    #endif
}

extern "C"
{
    EXPORT void InitTracker(float fx, float fy, float cx, float cy)
    {
        cameraMatrix = (
            cv::Mat_<double>(3, 3) << fx, 0, cx,
                                      0, fy, cy,
                                      0, 0, 1
        );
        distCoeffs = cv::Mat::zeros(1, 5, CV_64F); 

        #if defined(__ANDROID__)
            cv::aruco::Dictionary dict = cv::aruco::getPredefinedDictionary(cv::aruco::DICT_6X6_250);
            cv::aruco::DetectorParameters params = cv::aruco::DetectorParameters();
            arucoDetector = cv::makePtr<cv::aruco::ArucoDetector>(dict, params);
        #else
            dictionary = cv::aruco::getPredefinedDictionary(cv::aruco::DICT_6X6_250);
            detectorParams = cv::aruco::DetectorParameters::create();
        #endif

        LOGI("NativeCV: Tracker Initialized Successfully");
    }

    EXPORT bool ProcessFrame(
        unsigned char* imageData, int width, int height, float markerLengthMeters, 
        float* outTvec, float* outRvec
    )
    {
        if (cameraMatrix.empty())
        {
            LOGE("NativeCV: Camera matrix not initialized!");
            return false;
        }

        cv::Mat img(height, width, CV_8UC4, imageData);
        cv::Mat gray;
        cv::cvtColor(img, gray, cv::COLOR_RGBA2GRAY);
        cv::flip(gray, gray, 0);

        std::vector<int> markerIds;
        std::vector<std::vector<cv::Point2f>> markerCorners, rejectedCandidates;

        #if defined(__ANDROID__)
            if (arucoDetector.empty()) return false;
            arucoDetector->detectMarkers(gray, markerCorners, markerIds, rejectedCandidates);
        #else
            cv::aruco::detectMarkers(gray, dictionary, markerCorners, markerIds, detectorParams, rejectedCandidates);
        #endif

        for (size_t i = 0; i < markerIds.size(); i++)
        {
            if (markerIds[i] == 0)
            {
                
                float halfSize = markerLengthMeters / 2.0f;
                std::vector<cv::Point3f> objPoints = {
                    cv::Point3f(-halfSize, halfSize, 0),
                    cv::Point3f(halfSize, halfSize, 0),
                    cv::Point3f(halfSize, -halfSize, 0),
                    cv::Point3f(-halfSize, -halfSize, 0)
                };

                cv::Mat rvec, tvec;
                cv::solvePnP(objPoints, markerCorners[i], cameraMatrix, distCoeffs, rvec, tvec);

                outTvec[0] = (float)tvec.at<double>(0);
                outTvec[1] = (float)tvec.at<double>(1);
                outTvec[2] = (float)tvec.at<double>(2);

                outRvec[0] = (float)rvec.at<double>(0);
                outRvec[1] = (float)rvec.at<double>(1);
                outRvec[2] = (float)rvec.at<double>(2);

                return true;
            }
        }
        return false;
    }
}