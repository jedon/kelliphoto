# Gmail Email Setup Guide

## Overview

The contact page uses SMTP to send emails via Gmail. You'll need to create a **Gmail App Password** (not an API key) to authenticate.

## Gmail vs Google Workspace

**Recommended: Use Google Workspace (jedon@darklingdesign.com)**
- Better email deliverability
- More professional appearance
- Better support for business use
- Same setup process as regular Gmail

**Regular Gmail (jedon13@gmail.com)**
- Will work fine, but less professional
- Same setup process

## Setup Steps

### Step 1: Enable 2-Step Verification

1. Go to your Google Account: https://myaccount.google.com/
2. Navigate to **Security** → **2-Step Verification**
3. Enable 2-Step Verification if not already enabled
   - This is required to create App Passwords

### Step 2: Create an App Password

1. Go to your Google Account: https://myaccount.google.com/
2. Navigate to **Security** → **2-Step Verification**
3. Scroll down to **App passwords** (or go directly to: https://myaccount.google.com/apppasswords)
4. Select **Mail** as the app type
5. Select **Other (Custom name)** as the device
6. Enter a name like "KelliPhoto Contact Form"
7. Click **Generate**
8. **Copy the 16-character password** (you won't see it again!)

The password will look like: `abcd efgh ijkl mnop` (with spaces - remove spaces when using)

### Step 3: Configure appsettings.json

Add your Gmail credentials to `appsettings.json`:

```json
"Email": {
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": 587,
  "SmtpUsername": "jedon@darklingdesign.com",  // Your Gmail or Google Workspace email
  "SmtpPassword": "abcdefghijklmnop",           // The 16-character App Password (no spaces)
  "FromEmail": "jedon@darklingdesign.com",     // Email to show as "from"
  "FromName": "Kelli Thompson Photography",     // Name to show as sender
  "ContactEmail": "jedon@darklingdesign.com"   // Where to send contact form submissions
}
```

### Step 4: Secure Your Credentials

**For Production:**
- Use environment variables or a secrets manager
- Never commit passwords to Git
- In Docker, set environment variables in `docker-compose.yml`:

```yaml
environment:
  - Email__SmtpUsername=jedon@darklingdesign.com
  - Email__SmtpPassword=your_app_password_here
  - Email__ContactEmail=jedon@darklingdesign.com
```

### Step 5: Test the Contact Form

1. Start your application
2. Navigate to `/contact`
3. Fill out and submit the form
4. Check your email inbox for the contact form submission

## Troubleshooting

### "Invalid login" error
- Make sure 2-Step Verification is enabled
- Verify the App Password is correct (no spaces)
- Check that you're using the App Password, not your regular Gmail password

### "Connection timeout" error
- Verify firewall allows outbound connections to `smtp.gmail.com:587`
- Check if your network blocks SMTP ports

### Email not received
- Check spam/junk folder
- Verify `ContactEmail` is set correctly in configuration
- Check application logs for errors

## Security Notes

- **Never share your App Password** - treat it like your account password
- If compromised, immediately revoke the App Password and create a new one
- App Passwords can be revoked at any time from your Google Account settings
- Each App Password is unique to the app/device you created it for

## Alternative: Using Environment Variables

For Docker/production, you can set these via environment variables (using double underscores for nested config):

```bash
Email__SmtpUsername=jedon@darklingdesign.com
Email__SmtpPassword=your_app_password
Email__ContactEmail=jedon@darklingdesign.com
Email__FromEmail=jedon@darklingdesign.com
Email__FromName=Kelli Thompson Photography
```

These will override the values in `appsettings.json`.
