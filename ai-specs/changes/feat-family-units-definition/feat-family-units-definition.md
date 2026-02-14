# Family members and unit definition

## Objective

The objective of this feature is to allow users to define a family unit and members and their relationships within the ABUVI system. This will enable users to manage family-related information and interactions effectively, enhancing the overall user experience and providing a more comprehensive profile management system. This feature prepares information for future camping inscriptions and family-related needs.

## User Story

**As a** user of the ABUVI system
**I want to** be able to define family members and their relationships within the system
**So that** I can manage family-related information and interactions effectively

## Acceptance Criteria

### ✅ Family Member Definition

- [ ] Users can create a family unit and add family members to it
- [ ] Each user that creates a family unit is considered the "head" of that family unit and can manage the family member profiles within it
- [ ] User that creates a family unit is created as family member and automatically to family unit
- [ ] Users can specify for family unit these fields:
  - [ ] Name (string, required)
  - [ ] Photo (image file, optional)
- [ ] Users can create family member profiles with the following fields:
  - Name (string, required)
  - Photo (image file, optional)
  - Date of Birth (date, required)
  - Relationship to User (enum: Parent, Sibling, Child, Spouse, Other)
  - Document ID (string, optional)
  - Email Contact Information (string, optional)
  - Phone Contact Information (string, optional)
- [ ] Users can edit and delete family member profiles
- [ ] Family member profiles are linked to the user's account and can be accessed from the user's profile page
- [ ] Family member information is stored securely and complies with data privacy regulations

### ✅ Family Unit Management

- [ ] Users can view a list of their defined family members on their profile page
- [ ] Each family member can register in ABUVI application with their own user account
