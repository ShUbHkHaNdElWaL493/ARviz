#include <ament_index_cpp/get_package_share_directory.hpp>
#include <arpa/inet.h>
#include "arviz_desktop_server/httplib.h"
#include <assimp/Exporter.hpp>
#include <assimp/Importer.hpp>
#include <assimp/postprocess.h>
#include <rclcpp/rclcpp.hpp>

std::string get_local_ip()
{
  std::string ip = "127.0.0.1 (Localhost)";
  struct ifaddrs *interfaces = nullptr;
  if (getifaddrs(&interfaces) == 0) {
    for (struct ifaddrs *ifa = interfaces; ifa != nullptr; ifa = ifa->ifa_next) {
      if (ifa->ifa_addr != nullptr && ifa->ifa_addr->sa_family == AF_INET) {
        std::string current_ip = inet_ntoa(((struct sockaddr_in *)ifa->ifa_addr)->sin_addr);
        std::string name = ifa->ifa_name;
        if (current_ip != "127.0.0.1" && name.find("docker") == std::string::npos) {
          ip = current_ip;
          break;
        }
      }
    }
    freeifaddrs(interfaces);
  }
  return ip;
}

class ARvizDesktopServer : public rclcpp::Node
{
private:
  const std::unordered_set<std::string> allowed_extensions;
  httplib::Server server;
  std::thread server_thread;

  void serve_dae(const std::string & filepath, httplib::Response & res)
  {
    Assimp::Importer importer;
    const aiScene * scene = importer.ReadFile(filepath,
      aiProcess_Triangulate | aiProcess_GenNormals | aiProcess_FlipUVs);

    if (!scene) {
      RCLCPP_ERROR(this->get_logger(), "Assimp Import Error: %s", importer.GetErrorString());
      res.status = 500;
      res.set_content("Internal Server Error", "text/plain");
      return;
    }

    Assimp::Exporter exporter;
    const aiExportDataBlob * blob = exporter.ExportToBlob(scene, "glb2", aiProcess_FlipUVs);

    if (!blob) {
      RCLCPP_ERROR(this->get_logger(), "Assimp Export Error: %s", exporter.GetErrorString());
      res.status = 500;
      res.set_content("Internal Server Error", "text/plain");
      return;
    }

    std::string new_filename = std::filesystem::path(filepath).stem().string() + ".glb";
    res.set_header("Content-Disposition", "attachment; filename=\"" + new_filename + "\"");

    res.set_content(reinterpret_cast<const char *>(blob->data), blob->size, "model/gltf-binary");
  }

  void serve_stl(const std::string & filepath, httplib::Response & res)
  {
    std::string mime_type = "application/octet-stream";
    if (filepath.find(".stl") != std::string::npos) {
      mime_type = "model/stl";
    } else if (filepath.find(".obj") != std::string::npos) {
      mime_type = "model/obj";
    } else if (filepath.find(".glb") != std::string::npos) {
      mime_type = "model/gltf-binary";
    } else if (filepath.find(".gltf") != std::string::npos) {
      mime_type = "model/gltf+json";
    } else if (filepath.find(".png") != std::string::npos) {mime_type = "image/png";} else if (
      (filepath.find(".jpg") != std::string::npos) ||
      (filepath.find(".jpeg") != std::string::npos)) {mime_type = "image/jpeg";}

    res.set_file_content(filepath, mime_type);
  }

  void handle_request(const httplib::Request & req, httplib::Response & res)
  {
    std::string package_name = req.matches[1];
    std::string file_path = req.matches[2];

    try {
      std::string package_share_dir = ament_index_cpp::get_package_share_directory(package_name);
      std::filesystem::path absolute_path =
        std::filesystem::weakly_canonical(std::filesystem::path(package_share_dir) / file_path);
      std::filesystem::path base_path = std::filesystem::weakly_canonical(package_share_dir);

      if (absolute_path.string().find(base_path.string()) != 0) {
        RCLCPP_ERROR(this->get_logger(), "Forbidden path traversal attempt.");
        res.status = 403;
        res.set_content("Forbidden Path Traversal", "text/plain");
        return;
      }

      if (!std::filesystem::exists(absolute_path)) {
        RCLCPP_ERROR(this->get_logger(), "File not found: %s", absolute_path.c_str());
        res.status = 404;
        res.set_content("File Not Found", "text/plain");
        return;
      }

      if (allowed_extensions.find(absolute_path.extension().string()) == allowed_extensions.end()) {
        RCLCPP_ERROR(this->get_logger(), "Forbidden File Type Request: %s", absolute_path.c_str());
        res.status = 403;
        res.set_content("Forbidden File Type", "text/plain");
        return;
      }

      RCLCPP_INFO(this->get_logger(), "Serving: %s", absolute_path.c_str());

      if (absolute_path.extension() == ".dae" || absolute_path.extension() == ".DAE") {
        serve_dae(absolute_path.string(), res);
      } else {
        serve_stl(absolute_path.string(), res);
      }

    } catch (const std::exception & e) {
      RCLCPP_ERROR(this->get_logger(), "Error resolving package: %s", e.what());
      res.status = 500;
      res.set_content("Internal Server Error", "text/plain");
    }
  }

public:
  ARvizDesktopServer()
  :Node("desktop_server"),
    allowed_extensions({
    ".dae", ".DAE",
    ".stl", ".STL",
    ".obj", ".OBJ",
    ".glb", ".GLB",
    ".gltf", ".GLTF",
    ".png", ".PNG",
    ".jpg", ".JPG",
    ".jpeg", ".JPEG"
  })
  {
    int port = 8000;
    std::string local_ip = get_local_ip();
    RCLCPP_INFO(this->get_logger(), "=============================");
    RCLCPP_INFO(this->get_logger(), " Starting ARviz HTTP Server");
    RCLCPP_INFO(this->get_logger(), " IP Address: %s", local_ip.c_str());
    RCLCPP_INFO(this->get_logger(), " Port: %d", port);
    RCLCPP_INFO(this->get_logger(), "=============================");
    server.Get(R"(/assets/([^/]+)/(.*))",
      [&](const httplib::Request & req, httplib::Response & res) {
        handle_request(req, res);
        });
    server_thread = std::thread([this, port]() {
          server.listen("0.0.0.0", port);
        });
  }

  ~ARvizDesktopServer()
  {
    server.stop();
    if (server_thread.joinable()) {
      server_thread.join();
    }
  }
};

int main(int argc, char **argv)
{
  rclcpp::init(argc, argv);
  rclcpp::spin(std::make_shared<ARvizDesktopServer>());
  rclcpp::shutdown();
  return 0;
}
