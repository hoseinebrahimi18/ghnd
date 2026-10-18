# ساخت OpenSSTP ویندوز با موتور SoftEther همراه برنامه

1. محتویات این ZIP را در ریشهٔ مخزن GitHub قرار بده؛ فقط خود ZIP را به‌عنوان یک فایل آپلود نکن.
2. وارد تب **Actions** شو.
3. Workflow با نام **Build OpenSSTP Windows with bundled SoftEther engine** را انتخاب کن.
4. روی **Run workflow** بزن.
5. Workflow سورس رسمی SoftEther را با submoduleهای لازم دریافت و کامپایل می‌کند، فقط فایل‌های موتور (`vpnclient.exe`, `vpncmd.exe`, `hamcore.se2`) را کنار برنامه قرار می‌دهد و سپس OpenSSTP را منتشر می‌کند.
6. بعد از موفقیت، Artifact با نام `OpenSSTP-Windows-Bundled-SoftEther` را دانلود کن.

## چه چیزی نصب می‌شود؟

برنامهٔ رسمی **SoftEther VPN Client + VPN Gate Client Plugin** و رابط گرافیکی Client Manager همراه خروجی نیستند. در اولین اتصال، OpenSSTP با تأیید Administrator موتور سرویس SoftEther را در `%ProgramData%\OpenSSTP\SoftEtherRuntime` کپی می‌کند، سرویس ویندوز را ثبت می‌کند و آداپتور مجازی SoftEther را ایجاد می‌کند. این کار برای ساخت تونل واقعی VPN لازم است؛ هیچ VPN سیستمی نمی‌تواند بدون اجزای تونل و درایور/آداپتور لازم کار کند.

## وضعیت آزمایش

این ZIP شامل کد و Workflow برای ساخت از سورس است؛ در این محیط امکان اجرای build ویندوز، نصب درایور یا آزمایش اتصال به سرور زنده VPN Gate وجود ندارد. موفقیت Workflow، اتصال واقعی را به‌تنهایی اثبات نمی‌کند. پس از ساخت، اتصال، عبور ترافیک و نمودار باید روی ویندوز آزمایش شوند.
