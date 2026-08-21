#include <ament_index_cpp/get_package_share_directory.hpp>
#include "arviz_desktop_server/httplib.h"
#include <assimp/Exporter.hpp>
#include <assimp/Importer.hpp>
#include <assimp/postprocess.h>
#include <rclcpp/rclcpp.hpp>

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
    std::ifstream file(filepath, std::ios::binary);
    if (!file.is_open()) {
      res.status = 500;
      res.set_content("Internal Server Error", "text/plain");
      return;
    }
    std::string content((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());

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

    res.set_content(content, mime_type);
  }

  void handle_request(const httplib::Request & req, httplib::Response & res)
  {
    std::string package_name = req.matches[1];
    std::string file_path = req.matches[2];

    try {
      std::string package_share_dir = ament_index_cpp::get_package_share_directory(package_name);
      std::filesystem::path absolute_path = std::filesystem::path(package_share_dir) / file_path;

      if (!std::filesystem::exists(absolute_path)) {
        RCLCPP_ERROR(this->get_logger(), "File not found: %s", absolute_path.c_str());
        res.status = 404;
        res.set_content("File Not Found", "text/plain");
        return;
      }

      if (allowed_extensions.find(absolute_path.extension().string()) == allowed_extensions.end()) {
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
    allowed_extensions({".dae", ".stl", ".obj", ".glb", ".gltf", ".png", ".jpg", ".jpeg"})
  {
    int port = 8000;
    RCLCPP_INFO(this->get_logger(), "Starting ARviz HTTP Server on port %d...", port);
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
