UDM_10 – Multi-file Upload

1. Giới thiệu

UDM_10 là project mô phỏng hệ thống truyền nhiều file giữa Client và Server qua mạng TCP.

2. Cấu trúc project

Code/Client: chương trình phía Client.

Code/Server: chương trình phía Server.

Code/Shared: các lớp dùng chung giữa Client và Server.

DOCX: tài liệu báo cáo.

Extra: tài liệu và tài nguyên bổ sung.

PPTX: slide thuyết trình.

3. Công nghệ

C#

.NET

TCP Socket/Network Stream

JSON UTF-8 cho thông điệp điều khiển

4. Giao thức truyền tin

Thông điệp điều khiển sử dụng định dạng:

[4 byte độ dài theo Big Endian][JSON UTF-8]

Sau thông điệp yêu cầu upload, nội dung file được truyền dưới dạng raw bytes.

Các hằng số lệnh hiện có trong Protocol.cs gồm PING và UPLOAD.

5. Cấu hình

Client và Server sử dụng file appsettings.json để cấu hình địa chỉ Host, Port và các giới hạn liên quan.

Port mặc định: 9000.