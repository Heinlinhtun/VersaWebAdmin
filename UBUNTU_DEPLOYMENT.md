# VersaWebAdmin Ubuntu deployment guide

This guide redeploys all three server-facing parts on a fresh Ubuntu Server:

- `Versa.POS`: ASP.NET Core API, proxied as `https://api.example.com`
- `VersaWebAdmin`: ASP.NET Core admin app, proxied as `https://admin.example.com`
- `Versa.UI`: Vue/Vite frontend, served as static files at `https://app.example.com`

Replace these examples before running commands:

- `api.example.com`, `admin.example.com`, `app.example.com`
- `YOUR_REPO_URL`
- `STRONG_MYSQL_PASSWORD`
- `LONG_RANDOM_JWT_KEY`
- Dropbox and Telegram tokens if backups/alerts are enabled

The projects currently target `.NET 10` and the frontend uses Vite 7. Use Ubuntu 24.04 LTS or newer if possible.

## 1. Point DNS to the server

Create DNS `A` records for:

```text
api.example.com    -> your server public IP
admin.example.com  -> your server public IP
app.example.com    -> your server public IP
```

Wait until DNS resolves:

```bash
dig +short api.example.com
dig +short admin.example.com
dig +short app.example.com
```

## 2. Initial server setup

SSH into the new server, then update it:

```bash
sudo apt update
sudo apt upgrade -y
sudo apt install -y git curl wget ca-certificates gnupg lsb-release unzip nginx mysql-server mysql-client ufw certbot python3-certbot-nginx
```

Create a deployment user if you do not already have one:

```bash
sudo adduser deploy
sudo usermod -aG sudo deploy
sudo usermod -aG www-data deploy
```

Enable the firewall:

```bash
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx Full'
sudo ufw --force enable
sudo ufw status
```

## 3. Install .NET 10

Check the official Ubuntu/.NET package guidance if this changes later:

- Microsoft: https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install
- Ubuntu: https://ubuntu.com/developers/docs/howto/dotnet-setup/

On current Ubuntu releases, try:

```bash
sudo apt update
sudo apt install -y dotnet-sdk-10.0 aspnetcore-runtime-10.0
dotnet --info
```

If `dotnet-sdk-10.0` is not found, enable Ubuntu .NET backports:

```bash
sudo add-apt-repository -y ppa:dotnet/backports
sudo apt update
sudo apt install -y dotnet-sdk-10.0 aspnetcore-runtime-10.0
```

## 4. Install Node.js

Vite 7 needs a modern Node version. Node.js 22 LTS is a safe choice.

```bash
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash -
sudo apt install -y nodejs
node -v
npm -v
```

Reference: https://deb.nodesource.com/

## 5. Configure MySQL

Secure MySQL:

```bash
sudo mysql_secure_installation
```

Create the master database and an app user:

```bash
sudo mysql
```

```sql
CREATE DATABASE IF NOT EXISTS POS_Master CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'versa_app'@'localhost' IDENTIFIED BY 'STRONG_MYSQL_PASSWORD';
GRANT ALL PRIVILEGES ON POS_Master.* TO 'versa_app'@'localhost';
GRANT ALL PRIVILEGES ON `pos\_%`.* TO 'versa_app'@'localhost';
GRANT ALL PRIVILEGES ON pos_template.* TO 'versa_app'@'localhost';
FLUSH PRIVILEGES;
EXIT;
```

If you have a previous backup, restore it now. This is the recommended route:

```bash
mysql -u versa_app -p POS_Master < POS_Master_backup.sql
mysql -u versa_app -p pos_template < pos_template_backup.sql
mysql -u versa_app -p pos_db_shop1 < pos_db_shop1_backup.sql
```

If you do not have a backup, create the minimum master tables so the admin app can start:

```bash
mysql -u versa_app -p POS_Master
```

```sql
CREATE TABLE IF NOT EXISTS MasterUsers (
  UserId INT NOT NULL AUTO_INCREMENT,
  Username VARCHAR(100) NOT NULL UNIQUE,
  PasswordHash VARCHAR(255) NOT NULL,
  Role VARCHAR(50) NOT NULL,
  PRIMARY KEY (UserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Tenants (
  TenantId INT NOT NULL AUTO_INCREMENT,
  ShopName VARCHAR(255) NOT NULL,
  ShopLoginId VARCHAR(100) NOT NULL UNIQUE,
  DbName VARCHAR(100) NOT NULL UNIQUE,
  SubscriptionPlan VARCHAR(100) NULL,
  SubscriptionStartDate DATETIME NULL,
  SubscriptionEndDate DATETIME NULL,
  IsActive TINYINT(1) NOT NULL DEFAULT 1,
  MaxUsers INT NOT NULL DEFAULT 5,
  ShopType VARCHAR(50) NULL,
  Address VARCHAR(500) NULL,
  ContactNumber VARCHAR(100) NULL,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (TenantId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

EXIT;
```

Important: normal POS shop creation clones schema from `pos_template`. If `pos_template` is missing, non-restaurant shop creation will fail. Restore it from your old server if possible.

## 6. Clone the repo

```bash
sudo mkdir -p /var/www/versa
sudo chown deploy:www-data /var/www/versa
cd /var/www/versa
git clone YOUR_REPO_URL source
cd source
```

## 7. Publish the API and admin app

```bash
cd /var/www/versa/source
dotnet restore VersaWebAdmin.slnx

dotnet publish Versa.POS/Versa.POS.csproj -c Release -o /var/www/versa/api
dotnet publish VersaWebAdmin/VersaWebAdmin.csproj -c Release -o /var/www/versa/admin

sudo chown -R www-data:www-data /var/www/versa/api /var/www/versa/admin
```

## 8. Build and deploy the frontend

The current frontend API URL is hard-coded in `Versa.UI/src/services/api.js`:

```js
baseURL: "https://api.versapos.info/api"
```

Before building, edit it to your new API domain, for example:

```js
baseURL: "https://api.example.com/api"
```

Then build:

```bash
cd /var/www/versa/source/Versa.UI
npm ci
npm run build

sudo mkdir -p /var/www/versa/frontend
sudo rsync -a --delete dist/ /var/www/versa/frontend/
sudo chown -R www-data:www-data /var/www/versa/frontend
```

## 9. Create systemd services

Create `/etc/systemd/system/versa-api.service`:

```ini
[Unit]
Description=Versa POS API
After=network.target mysql.service

[Service]
WorkingDirectory=/var/www/versa/api
ExecStart=/usr/bin/dotnet /var/www/versa/api/Versa.POS.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=versa-api
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5021
Environment=ConnectionStrings__MasterDbConnection=Server=127.0.0.1;Port=3306;Database=POS_Master;Uid=versa_app;Pwd=STRONG_MYSQL_PASSWORD;AllowUserVariables=True;PersistSecurityInfo=True;CharSet=utf8mb4;
Environment=Jwt__Key=LONG_RANDOM_JWT_KEY

[Install]
WantedBy=multi-user.target
```

Create `/etc/systemd/system/versa-admin.service`:

```ini
[Unit]
Description=Versa Web Admin
After=network.target mysql.service

[Service]
WorkingDirectory=/var/www/versa/admin
ExecStart=/usr/bin/dotnet /var/www/versa/admin/VersaWebAdmin.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=versa-admin
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5155
Environment=ConnectionStrings__MasterDbConnection=Server=127.0.0.1;Port=3306;Database=POS_Master;Uid=versa_app;Pwd=STRONG_MYSQL_PASSWORD;AllowUserVariables=True;PersistSecurityInfo=True;CharSet=utf8mb4;
Environment=DropboxBackup__Enabled=true
Environment=DropboxBackup__RefreshToken=YOUR_DROPBOX_REFRESH_TOKEN
Environment=DropboxBackup__AppKey=YOUR_DROPBOX_APP_KEY
Environment=DropboxBackup__AppSecret=YOUR_DROPBOX_APP_SECRET
Environment=TelegramAlerts__Enabled=true
Environment=TelegramAlerts__BotToken=YOUR_TELEGRAM_BOT_TOKEN
Environment=TelegramAlerts__ChatId=YOUR_TELEGRAM_CHAT_ID

[Install]
WantedBy=multi-user.target
```

If you do not want Dropbox or Telegram yet, set their `Enabled` values to `false`.

Start both services:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now versa-api versa-admin
sudo systemctl status versa-api --no-pager
sudo systemctl status versa-admin --no-pager
```

View logs:

```bash
journalctl -u versa-api -f
journalctl -u versa-admin -f
```

## 10. Configure Nginx

Create `/etc/nginx/sites-available/versa-api`:

```nginx
server {
    listen 80;
    server_name api.example.com;

    location / {
        proxy_pass http://127.0.0.1:5021;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Create `/etc/nginx/sites-available/versa-admin`:

```nginx
server {
    listen 80;
    server_name admin.example.com;

    location / {
        proxy_pass http://127.0.0.1:5155;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Create `/etc/nginx/sites-available/versa-frontend`:

```nginx
server {
    listen 80;
    server_name app.example.com;

    root /var/www/versa/frontend;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Enable the sites:

```bash
sudo ln -s /etc/nginx/sites-available/versa-api /etc/nginx/sites-enabled/
sudo ln -s /etc/nginx/sites-available/versa-admin /etc/nginx/sites-enabled/
sudo ln -s /etc/nginx/sites-available/versa-frontend /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

## 11. Add HTTPS certificates

```bash
sudo certbot --nginx -d api.example.com -d admin.example.com -d app.example.com
sudo certbot renew --dry-run
```

## 12. Verify deployment

Check backend ports locally:

```bash
curl -I http://127.0.0.1:5021
curl -I http://127.0.0.1:5155
```

Check public URLs:

```bash
curl -I https://api.example.com
curl -I https://admin.example.com
curl -I https://app.example.com
```

Open:

- `https://admin.example.com`
- `https://app.example.com`

On a fresh master database, the admin app creates this default admin when `MasterUsers` is empty:

```text
username: admin
password: Admin@123
```

Change it immediately after first login.

## 13. Redeploy after code changes

```bash
cd /var/www/versa/source
git pull
dotnet restore VersaWebAdmin.slnx

sudo systemctl stop versa-api versa-admin
dotnet publish Versa.POS/Versa.POS.csproj -c Release -o /var/www/versa/api
dotnet publish VersaWebAdmin/VersaWebAdmin.csproj -c Release -o /var/www/versa/admin
sudo chown -R www-data:www-data /var/www/versa/api /var/www/versa/admin
sudo systemctl start versa-api versa-admin

cd /var/www/versa/source/Versa.UI
npm ci
npm run build
sudo rsync -a --delete dist/ /var/www/versa/frontend/
sudo chown -R www-data:www-data /var/www/versa/frontend
sudo systemctl reload nginx
```

## 14. Common problems

API returns 502:

```bash
sudo systemctl status versa-api --no-pager
journalctl -u versa-api -n 100 --no-pager
```

Admin returns 502:

```bash
sudo systemctl status versa-admin --no-pager
journalctl -u versa-admin -n 100 --no-pager
```

MySQL login fails:

```bash
mysql -u versa_app -p POS_Master
```

Frontend login calls the old API:

- Recheck `Versa.UI/src/services/api.js`.
- Rebuild with `npm run build`.
- Redeploy `dist/` to `/var/www/versa/frontend`.

New POS shop registration fails:

- Confirm `pos_template` exists.
- Confirm the MySQL user has privileges on `pos_template` and `pos_%`.
- Restaurant shops clone the live `restaurant_template` database; normal POS shops clone `pos_template`.

Backups fail:

- Install `mysql-client`, because admin backup uses `mysqldump`.
- Check Dropbox environment variables.
- Check logs with `journalctl -u versa-admin -f`.

## 15. Security checklist

- Rotate the committed MySQL password, Dropbox token, Telegram token, and JWT key.
- Prefer environment variables/systemd overrides over editing production secrets into `appsettings.json`.
- Change the default `admin / Admin@123` login immediately.
- Keep MySQL bound to localhost unless you intentionally need remote database access.
- Run `sudo apt update && sudo apt upgrade` regularly.
