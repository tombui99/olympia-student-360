# Student 360 Microsoft 365 sign-in

Microsoft Entra verifies the user's school identity. A Student 360 administrator links an existing account to the school's tenant ID and the user's Entra Object ID. Match users by their school email during setup, then use the Object ID from Olympia IT for the actual link. The existing Student 360 account supplies the user's role and data access.

## Message for Olympia IT

Chào anh/chị IT Olympia, nhờ anh/chị hỗ trợ cấu hình đăng nhập Microsoft 365 cho Student 360. Bên em đề xuất thử nghiệm trước với một vài tài khoản đã có trong Student 360.

1. Trong **Microsoft Entra admin center → App registrations → New registration**, tạo ứng dụng **Olympia Student 360**. Chọn **Accounts in this organizational directory only** (single tenant). Trong **Authentication**, thêm Redirect URI dưới nền tảng **Web**:

   `https://s360.theolympiaschools.edu.vn/api/auth/microsoft/callback`

   Ứng dụng dùng OpenID Connect với authorization code flow và PKCE. Có thể giữ tắt các lựa chọn **Implicit grant and hybrid flows**.
2. Trong **API permissions**, thêm quyền delegated **openid** và **profile** của Microsoft Graph. Nếu có quyền mặc định **User.Read**, anh/chị có thể gỡ quyền này trước khi cấp consent. Nhờ quản trị viên cấp **Grant admin consent** cho hai quyền đăng nhập. Trong **Enterprise applications → Olympia Student 360 → Properties**, bật **Assignment required**, sau đó vào **Users and groups** để chỉ định người dùng thử nghiệm.
3. Gửi bên em **Directory (tenant) ID**, **Application (client) ID**, cùng **UPN/email đăng nhập Microsoft 365** và **Entra user Object ID** của từng người thử nghiệm. Nếu email trong Student 360 khác UPN, vui lòng ghi thêm email dùng trong Student 360. Bên em sẽ liên kết đúng tài khoản hiện có.
4. Để chạy thử, vào **Certificates & secrets → Client secrets** và tạo một client secret. Nhờ anh/chị chia sẻ **secret Value** và ngày hết hạn qua kênh bảo mật đã thống nhất. Microsoft chỉ hiển thị Value lúc tạo. Nếu chính sách của trường yêu cầu dùng chứng chỉ thay cho client secret, báo bên em trước khi tạo credential để hai bên thống nhất phương án.

Sau khi cấu hình xong, bên em sẽ phối hợp thử đăng nhập và kiểm tra quyền truy cập. Cảm ơn anh/chị!

[App registration](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app) · [Web redirect URI](https://learn.microsoft.com/en-us/entra/identity-platform/how-to-add-redirect-uri) · [Assignment and admin consent](https://learn.microsoft.com/azure/active-directory/develop/howto-restrict-your-app-to-a-set-of-users) · [App credentials](https://learn.microsoft.com/entra/identity-platform/how-to-add-credentials)

## Student 360 deployment and pilot

1. Deploy the API and frontend using the existing workflows. The API migration creates the shared data protection key table so the sign-in request and callback can be processed by different App Runner instances. Limit database access and protect the database and its backups, which hold the key ring.
2. In App Runner, configure the following environment variables:

   | Name | Value |
   | --- | --- |
   | `MicrosoftSignIn__TenantId` | Olympia's Directory (tenant) ID |
   | `MicrosoftSignIn__ClientId` | The Entra Application (client) ID |
   | `MicrosoftSignIn__PublicOrigin` | `https://s360.theolympiaschools.edu.vn` |
   | `MicrosoftSignIn__ClientSecret` | Reference the client secret from AWS Secrets Manager or SSM Parameter Store |
   | `MicrosoftSignIn__Enabled` | `true` after the other settings are present |

   Keep the client secret out of source control and chat. Give the App Runner instance role read access to that secret only. App Runner loads a rotated secret on redeployment. The service needs HTTPS access to `login.microsoftonline.com`. If it uses a VPC connector, verify that the connector's network has working internet egress.
3. Keep the CloudFront `/api/*` behavior pointed at the API with caching disabled and viewer cookies and query strings forwarded. The deployed distribution uses `CachingDisabled` and `AllViewerExceptHostHeader`. The API sets the redirect URI from `PublicOrigin` because CloudFront replaces the Host header on the origin request.
4. Sign in with an existing Student 360 administrator account. In **Account Management**, search by email, edit the pilot account, paste the **Entra user Object ID**, and click **Link Microsoft account**. Link only the people Olympia IT verified. An administrator can unlink a Microsoft identity to end that user's Microsoft sign-in sessions in Student 360.
5. From a private browser window on the school hostname, test a linked pilot account and verify its existing access. For the application-side denial test, have Olympia IT temporarily assign an approved test user in Entra who has no link in Student 360. Verify that Student 360 denies the user. Check that password login works and that signing out of Student 360 clears the app session. Microsoft sign-in sessions in Student 360 expire after one hour.

If the pilot needs to stop, set `MicrosoftSignIn__Enabled=false` in App Runner and deploy the updated settings. Entra links stay in the database for the next pilot.

[App Runner secrets](https://docs.aws.amazon.com/apprunner/latest/dg/env-variable.html) · [App Runner VPC networking](https://docs.aws.amazon.com/apprunner/latest/dg/network-vpc.html) · [CloudFront forwarding policy](https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/using-managed-origin-request-policies.html)
