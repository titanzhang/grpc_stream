GridFS
* 16MB limit of MongoDB document
* Split file into small chunks, and store chunks in multiple documents
* Documents: File chunks, File metadata
* MongoDB driver functionality

Performance Test
* Writing speed of GridFS + gRPC vs. Samba
* Specs
  - 8 CPU cores, 64GB RAM, SSD
  - Windows10 64bit
  - Local systems(gRPC server, MongoDB, Samba shared folder)
* Results
  - gRPC + GridFS: 25 MB/s
  - Samba: 2127 MB/s
  - GridFS: 27 MB/s
  - Local FS: 2923 MB/s
* Why
  - Docker/WSL?
  - MongoDB configuration?


Commands:
* gRPC + GridFS: dotnet run -- write -s104857600 -k/temp/perf_test_100m -mgrpc -r1
* Sambe: dotnet run -- write -s104857600 -kz:/perf_test_100m -mdisk -r1
* GridFS: dotnet run -- write -s104857600 -k/temp/perf_test_100m -mgridfs -r1
* disk: dotnet run -- write -s104857600 -k./temp/perf_test_100m -mdisk -r1

