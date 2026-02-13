# Resend integration for Abuvi

This spec outlines the integration of Resend, a service for sending transactional emails, into the Abuvi application. The goal is to enhance our email capabilities, allowing for more reliable and feature-rich email communications with our users.

## Objectives

- Integrate Resend as the primary email service for transactional emails.
- Migrate existing email service to Resend.

## Scope

- All transactional emails (e.g., registration confirmations, password resets) will be sent through Resend.
- Admin interface for managing email templates and monitoring email delivery will be implemented.
- Ensure that all email-related features are fully functional and tested after integration.

## Current transactional emails

- Welcome email
- Email verification
- Camp registration confirmation
- Password reset
- Camp updates and notifications
- Payment receipts
- Feedback requests
- Event reminders

## Resend features to leverage

- Template management: Create and manage email templates directly in Resend.
- Analytics: Monitor email delivery, open rates, and click-through rates.
