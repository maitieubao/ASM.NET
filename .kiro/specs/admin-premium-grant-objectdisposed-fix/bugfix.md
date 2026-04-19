# Bugfix Requirements Document

## Introduction

This document specifies the requirements for fixing the `ObjectDisposedException` that occurs when administrators attempt to grant or revoke premium subscriptions for users through the AdminUser interface. The exception prevents admins from managing user premium status, which is a critical administrative function. The root cause is related to DbContext or transaction disposal occurring before asynchronous database operations complete.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN admin clicks "Grant Premium" button and selects a subscription plan (1 month, 1 year, or lifetime) THEN the system throws `System.ObjectDisposedException: Cannot access a disposed object. Object name: 'System.Threading.ManualResetEventSlim'` at `GenericRepository.GetByIdAsync` line 22

1.2 WHEN admin clicks "Revoke Premium" button for a user with active premium THEN the system throws `System.ObjectDisposedException: Cannot access a disposed object. Object name: 'System.Threading.ManualResetEventSlim'` at `GenericRepository.GetByIdAsync` line 22

1.3 WHEN the ObjectDisposedException occurs THEN the premium grant/revoke operation fails completely and no database changes are persisted

1.4 WHEN the exception is thrown THEN the admin sees an error page instead of being redirected back to the user list with a success message

### Expected Behavior (Correct)

2.1 WHEN admin clicks "Grant Premium" button and selects a subscription plan (1 month, 1 year, or lifetime) THEN the system SHALL successfully grant premium to the user, create or update the UserSubscription record, set user.IsPremium to true, commit the transaction, and redirect to the user list with success message "Đã cấp quyền Premium theo gói đăng ký đã chọn."

2.2 WHEN admin clicks "Revoke Premium" button for a user with active premium THEN the system SHALL successfully revoke premium from the user, set user.IsPremium to false, deactivate all active UserSubscription records, commit the transaction, and redirect to the user list with success message "Đã thu hồi quyền Premium của người dùng."

2.3 WHEN premium grant or revoke operations are executed THEN the system SHALL ensure the DbContext and transaction remain valid throughout the entire async operation chain until SaveChangesAsync completes

2.4 WHEN database operations complete successfully THEN the system SHALL properly dispose of transactions and DbContext only after all async operations have finished

### Unchanged Behavior (Regression Prevention)

3.1 WHEN admin performs any other user management operations (view users, search users, view user details, lock/unlock users, delete users) THEN the system SHALL CONTINUE TO function without ObjectDisposedException

3.2 WHEN non-admin users interact with the application (login, play music, create playlists, etc.) THEN the system SHALL CONTINUE TO function normally without being affected by this fix

3.3 WHEN premium grant/revoke operations fail due to invalid user ID or invalid plan ID THEN the system SHALL CONTINUE TO return false and display appropriate error messages without throwing exceptions

3.4 WHEN transactions are rolled back due to exceptions THEN the system SHALL CONTINUE TO properly clean up resources and maintain database consistency

3.5 WHEN the system queries subscription plans for the admin UI dropdown THEN the system SHALL CONTINUE TO return only the 3 active plans (1 month, 1 year, lifetime) ordered by duration
