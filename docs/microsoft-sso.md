# Student 360 Microsoft 365 sign-in

Microsoft Entra verifies the user's school identity. When an administrator creates or edits an account with a school email, Student 360 looks it up in Microsoft Graph and links a unique matching Entra member. The API also backfills existing accounts when it starts and retries pending accounts every 15 minutes. Student 360 stores the tenant and Object ID for sign-in. The existing Student 360 account supplies the user's role and data access.

## Message for Olympia IT

Chào anh/chị IT Olympia, nhờ anh/chị hỗ trợ cấu hình đăng nhập Microsoft 365 cho Student 360. Bên em đề xuất thử nghiệm trước với một vài tài khoản đã có trong Student 360.

1. Trong **Microsoft Entra admin center → App registrations → New registration**, tạo ứng dụng **Olympia Student 360**. Chọn **Accounts in this organizational directory only** (single tenant). Trong **Authentication**, thêm Redirect URI dưới nền tảng **Web**:

   `https://s360.theolympiaschools.edu.vn/api/auth/microsoft/callback`

   Ứng dụng dùng OpenID Connect với authorization code flow và PKCE. Có thể giữ tắt các lựa chọn **Implicit grant and hybrid flows**.
2. Trong **API permissions**, thêm quyền delegated **openid** và **profile** để đăng nhập, cùng quyền **Microsoft Graph → Application permissions → User.Read.All** để Student 360 đối chiếu email khi người dùng đăng nhập lần đầu. Nhờ quản trị viên chọn **Grant admin consent**. Nếu **Assignment required** đang bật, vào **Enterprise applications → Olympia Student 360 → Users and groups** để cấp quyền cho nhân sự được phép dùng hệ thống. Có thể cấp theo nhóm nếu gói Entra hỗ trợ.
3. Email lưu trong Student 360 cần khớp với email hoặc UPN Microsoft 365 của từng người. Student 360 tự tra cứu Object ID.
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
   | `MicrosoftSignIn__AutoLinkByEmail` | Optional first sign-in fallback for eligible accounts still awaiting the backfill |

   Keep the client secret out of source control and chat. Give the App Runner instance role read access to that secret only. App Runner loads a rotated secret on redeployment. The service needs HTTPS access to `login.microsoftonline.com` for sign-in and `graph.microsoft.com` for email lookup. [Microsoft Graph permissions for listing users](https://learn.microsoft.com/en-us/graph/api/user-list?view=graph-rest-1.0)
3. Keep the CloudFront `/api/*` behavior pointed at the API with caching disabled, POST requests enabled, and viewer cookies forwarded. The deployed distribution uses `CachingDisabled` and `AllViewerExceptHostHeader`. Microsoft sends the authorization response in a form POST because its code can exceed the AWS WAF query string limit. The API sets the redirect URI from `PublicOrigin` because CloudFront replaces the Host header on the origin request.
4. Confirm that existing Student 360 accounts have the current school email, including privileged accounts. Graph must return a unique matching Entra member and Student 360 must have a single corresponding account with an assigned role. Check App Runner logs for aggregate backfill results. If Graph consent is pending, the backfill pauses and retries after 15 minutes. Ordinary account saves still succeed and show a Microsoft setup message.
5. Changing the saved email removes the old Microsoft link and ends its sessions before Student 360 attempts to link the new address. To stop Microsoft access for an account, select **Block Microsoft sign-in** and save. Clearing the checkbox and saving allows a new link.
6. From a private browser window on the school hostname, test a backfilled pilot account and confirm its existing role. Also test with an approved Entra member who has no Student 360 account. Verify that Student 360 denies the sign-in. Check that password login works and that signing out of Student 360 clears the app session. Microsoft sign-in sessions in Student 360 expire after one hour.

If the pilot needs to stop, set `MicrosoftSignIn__Enabled=false` in App Runner and deploy the updated settings. Entra links stay in the database for the next pilot.

[App Runner secrets](https://docs.aws.amazon.com/apprunner/latest/dg/env-variable.html) · [App Runner VPC networking](https://docs.aws.amazon.com/apprunner/latest/dg/network-vpc.html) · [CloudFront forwarding policy](https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/using-managed-origin-request-policies.html)
