# Specs for user experience improvements around web app

## Summary

This document outlines the specifications for implementing user experience improvements in our web application. The goal is to enhance the overall usability and satisfaction of our users by making the interface more intuitive, responsive, and visually appealing.

## Key Areas of Improvement

1. **Navigation**: Simplify the navigation structure to make it easier for users to find what they are looking for. This may involve reorganizing menus, adding breadcrumbs, and improving the search functionality.
2. **Responsiveness**: Ensure that the web application is fully responsive and works well on various devices, including desktops, tablets, and smartphones. This may involve using responsive design techniques and testing the application on different screen sizes.
3. **Visual Design**: Update the visual design of the application to make it more modern and appealing. This may involve updating the color scheme, typography, and overall layout of the application.

### Improvement 1: Camp editions definition

- **Description**: Redesign the camp edition definition process to be more user-friendly and efficient. This may involve creating a step-by-step wizard for defining camp editions, with clear instructions and validation at each step.
- Each year's camp edition would be in second half of August and would have a start and end date. So, the default start date and end date for a new camp edition would be pre-populated with the previous year's dates, but editable by the user. This would allow for quick setup of new editions while still providing flexibility to adjust dates as needed.
- The field `Motivo de la propuesta` is not required. CampEditions can be created without filling this field, and it can be left blank if the user does not have a specific reason for proposing the edition, maybe `Board` members are evaluating options or they just want to create the finally approved edition without going through the proposal process, so they can skip filling this field.
- `Notas adicionales` field is not necessary for the camp edition definition process, so it can be removed from the form to simplify the user interface and focus on the essential information needed to define a camp edition.

## Improvement 2: Selecting an element on a list and showing its details

- **Description**: Implement a feature that allows users to click an element in its name from a list (e.g., a camp edition, a member, etc.) and view its details in a dedicated section or modal. This would provide users with quick access to relevant information without having to navigate away from the current page.

## Improvement 4: Camp locations maps: Extend map vertically and add more details

- **Description**: Extend the map of camp locations vertically to provide more space for displaying additional details about each location. This may involve adding markers or pop-ups that show information such as the name of the location, last edition year.

## Improvement 3: Change static images to carousels with more images and details

- **Description**: Replace static images in the application with carousels that allow users to view multiple images and a description or name about each item. For example, instead of showing a single image for each camp edition, we could have a carousel that displays multiple images from that edition along with relevant details such as the edition year, location, and highlights.

## Improvement 5: Extend width of my profile page or add tabs to separate sections

- **Description**: Extend the width of the profile page to provide more space for displaying user information and related content on desktop. Alternatively, we could implement a tabbed interface that separates different sections of the profile (e.g., personal information, camp history, preferences) to improve organization and readability.
