# \# Visual Inspection Training System

# 

# A WPF-based Visual Inspection Training application designed for operator qualification, training, and performance analysis.

# 

# \## Features

# 

# \- User Authentication

# \- Quiz Engine

# \- Image Classification

# \- MySQL Integration

# \- Dashboard

# \- Reports

# \- Admin Review

# 

# \## Technology

# 

# \- C#

# \- WPF

# \- .NET Framework 4.8.1

# \- MVVM

# \- MySQL

# 

# \## Requirements

# 

# Visual Studio 2026

# 

# MySQL 8+

# 

# .NET Framework 4.8.1

# 

# \## Build

# 

# Open

# 

# VisualInpsectionTrainingSystem.slnx

# 

# Press

# 

# Ctrl + Shift + B

# 

# \## Local database configuration

# 

# Real MySQL credentials must not be committed to Git.

# 

# To configure a workstation:

# 

# 1. Copy `App.local.config.example` to `App.local.config`.

# 2. Replace `YOUR_LOCAL_PASSWORD` with your local MySQL password.

# 3. Keep the database name as `visualinspectionquiz` unless your local database is intentionally different.

# 4. Confirm `App.local.config` stays untracked by Git.

# 

# The application loads MySQL credentials in this order:

# 

# 1. `VITS_MYSQL_CONNECTION` full connection string environment variable.

# 2. `App.local.config` in the application folder or project folder.

# 3. The safe tracked fallback in `App.config`.

# 

# Optional environment overrides:

# 

# - `VITS_MYSQL_USER`

# - `VITS_MYSQL_PASSWORD`

# 

# If no local password is configured, startup shows a clear MySQL configuration error.

