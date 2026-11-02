# Phản hồi khách hàng về chức năng Học bạ

> Ghi chú nội bộ: nội dung bên dưới được soạn để gửi sau khi triển khai lên môi trường khách hàng. Đã kiểm tra mã nguồn, kiểm thử và bổ sung trường ngày thi tốt nghiệp riêng cho bản IB. Chưa đối soát trực tiếp ba hồ sơ được nêu; nhà trường vẫn cần xác nhận dữ liệu chính thức còn thiếu. Không thông báo rằng điểm IB hay khối của từng học sinh đã được xác nhận khi chưa thực hiện bước này.

Kính gửi thầy/cô, Bifrost đã cập nhật chức năng Học bạ theo các nội dung góp ý như sau:

| Mục góp ý | Nội dung | Phản hồi |
| --- | --- | --- |
| 1 & 6 | GPA của Phan Anh khi chưa đủ điểm | Đã điều chỉnh điều kiện lấy điểm và tính GPA. Năm học chưa đủ kết quả hợp lệ được để trống GPA; hệ thống không tự điền 4.2 hoặc dùng riêng điểm học kỳ 1 làm kết quả cả năm. GPA học tập sử dụng các năm được chọn có GPA năm hợp lệ. |
| 1 & 6 | Khối của Phan Anh theo năm học | Đã sửa cách xác định khối theo lớp/khóa học của từng năm, tránh áp khối hiện tại cho các năm trước. Có thể điều chỉnh khối theo năm trong “Chỉnh sửa bảng điểm”. Nhờ nhà trường đối soát Phan Anh: Khối 11 năm 2025–2026, Khối 12 năm 2026–2027. |
| 2 | Tín chỉ của môn mới có điểm học kỳ 1 | Môn cả năm mặc định là 1 tín chỉ, không tự giảm còn 0,5 khi thiếu học kỳ 2. Môn thực sự có thời lượng nửa năm có thể được đặt 0,5 tín chỉ. GPA được tính theo trọng số tín chỉ. Nhờ nhà trường kiểm tra lại trường hợp Thái Vũ Minh Châu. |
| 3 | Tên môn trên bảng điểm song ngữ | Đã chuẩn hóa tên môn tiếng Việt/tiếng Anh trên bản xem và PDF, bỏ tên khóa học nguồn và mã lớp như “11E1.1” khỏi tên môn chính thức. |
| 4 | Điểm học kỳ 1 xuất hiện ở cột cả năm | Đã bỏ cơ chế lấy một học kỳ thay cho kết quả năm. Cột YR chỉ có điểm khi đủ hai học kỳ hoặc có kết quả cả năm đã hoàn tất; nếu chỉ có học kỳ 1, ô cả năm được để trống. |
| 5 | Dòng điểm trung bình các môn từ 2024–2025 | Đã mặc định ẩn dòng điểm trung bình các môn trên bản song ngữ thang điểm 10 từ năm học 2024–2025. Nhà trường có thể bật/tắt riêng theo học sinh và năm học; áp dụng cho bản xem và PDF. |
| IB — Phạm Duy Long | PDF thiếu điểm, chưa thể rà soát | Đã bổ sung nhập/lưu HL/SL, điểm dự đoán, điểm đánh giá bốn kỳ và điểm cộng TOK/EE để đưa vào bản xem/PDF IB. Nhờ nhà trường cung cấp hoặc xác nhận dữ liệu chính thức của Phạm Duy Long. Ô chưa có dữ liệu vẫn để trống; hệ thống không tự quy đổi điểm Canvas thành điểm IB chính thức. Tổng dự đoán chỉ hiện khi đủ sáu môn có điểm, HL/SL và điểm cộng TOK/EE. |
| 7 | Căn lề bảng thang điểm ở mặt sau bản Quốc tế GPA | Đã chỉnh lại độ rộng ô, khoảng cách và vị trí nội dung bảng thang điểm, gồm phần “10.0 GPA Scale”, để tránh lệch và chồng chữ. |
| 8 | Căn trái các cột S1F, S2F, YR | Đã căn trái tiêu đề và giá trị các cột S1F, S2F, YR trên bản GPA song ngữ, Quốc tế và phần bảng điểm tương ứng của IB. |
| Tên học sinh | Thứ tự tên trên GPA song ngữ, Quốc tế và IB | Đã áp dụng thứ tự tên gọi + tên đệm + họ, bỏ dấu: Thái Vũ Minh Châu → Chau Vu Minh Thai; Vũ Minh Châu → Chau Minh Vu; Phan Anh → Anh Phan. Có trường tên chính thức để chỉnh các trường hợp đặc biệt. Bản song ngữ thang điểm 10 giữ họ tên tiếng Việt. |
| Lọc môn | Lọc theo khối và chọn tất cả | Đã bổ sung bộ lọc môn theo khối cùng thao tác chọn/bỏ chọn tất cả môn đang lọc. Thao tác chỉ ảnh hưởng danh sách đang lọc; lựa chọn được áp dụng cho bản xem và PDF. |
| 9 | Tên môn tiếng Việt còn xuất hiện trên GPA và IB | Đã chuẩn hóa cách lấy tên tiếng Anh cho bản GPA song ngữ, Quốc tế và IB. Với môn chưa khớp danh mục hoặc cần tên chính thức riêng, nhà trường có thể sửa tên tiếng Anh/tiếng Việt theo từng năm học trong thông tin bảng điểm. |
| 10 | Tách mã lớp khỏi tên môn | Đã tách mã lớp khỏi tên môn, bao gồm “Academic English / Tiếng Anh học thuật 11E1.1”. Tên môn chính thức giữ nội dung học thuật và không in kèm mã lớp. |
| 11 — GPA | Ghi chú và ngày tốt nghiệp | Đã bổ sung lưu ngày tốt nghiệp và ghi chú trên bản GPA song ngữ/Quốc tế, ví dụ “Học tại trường từ Khối 9”. Nội dung dài được chuyển trang để tránh chồng bảng điểm hoặc phần ký tên. |
| 11 — IB | Thông tin nhập học và ngày thi tốt nghiệp | Đã bổ sung ngày nhập học và ngày thi tốt nghiệp riêng trên bản xem/PDF IB, cùng ngày tốt nghiệp, ngày công bố điểm IB và ghi chú. Các ngày được lưu riêng, do nhà trường nhập/xác nhận theo hồ sơ chính thức. |
| 12 | Một số tên môn bị mất chữ | Đã điều chỉnh độ rộng, khoảng cách cột, xuống dòng và chuyển trang cho tên môn dài; kiểm tra hiển thị ký tự tiếng Việt trên PDF. |
| 13 | Nhóm môn hoạt động và kết quả Đạt/Passed | Đã nhóm Bóng đá, Bóng rổ, Gym, Bóng chuyền thành Thể dục/Physical Education; Nhạc cụ dân tộc, Guitar, Piano, Thanh nhạc, Hợp xướng, Trống thành Âm nhạc/Music; Mĩ thuật, Thiết kế đồ họa thành Mĩ thuật/Fine Arts. Bản song ngữ thang điểm 10 hiển thị Đạt/Passed hoặc Chưa đạt/Not passed thay điểm số; các môn này không tính vào GPA. |

Các cập nhật bổ sung hỗ trợ thao tác:

| Cập nhật | Phản hồi |
| --- | --- |
| Chỉnh sửa trực tiếp trên bảng điểm | Chọn “Chỉnh sửa bảng điểm” hoặc “Chỉnh sửa bảng điểm IB” ngay trong bản xem. Tên môn và tín chỉ được sửa tại dòng môn; điểm IB sửa trực tiếp trong bảng IB; khối theo năm, ngày tháng và ghi chú sửa tại phần tương ứng. Bấm Lưu để áp dụng hoặc Hủy để bỏ thay đổi. Dữ liệu lưu riêng, được giữ khi đồng bộ Canvas và không ghi thay đổi về Canvas. Điểm Canvas chỉ được xem; tín chỉ có thể ảnh hưởng GPA của bảng điểm. |
| Truy cập bảng điểm cá nhân | Đưa tab “Individual student” lên trước và mở danh sách học sinh mặc định khi vào Học bạ. Chọn học sinh để xem/xuất bảng điểm cho các năm cần thiết. |
| Tải tổng hợp nhanh hơn | Đã tối ưu tải dữ liệu. Khi xem tổng hợp, chọn năm học và khối rồi bấm “Xem tổng hợp”; hệ thống xử lý phạm vi được chọn thay vì tự tải toàn trường. |
| Thang điểm mặc định | Mặc định chọn Percentage ở các trang có bộ chuyển thang điểm: bảng điểm cá nhân, tổng hợp và chi tiết khối. Vẫn có thể chuyển sang GPA 4.2 hoặc Letter grade. Các mẫu PDF giữ thang điểm theo loại bảng điểm. |
