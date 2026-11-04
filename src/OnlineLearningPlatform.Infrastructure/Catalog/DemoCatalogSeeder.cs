using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

/// <summary>Seeds a substantial local catalogue for the Development Compose stack only.</summary>
public sealed class DemoCatalogSeeder(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    IConfiguration configuration,
    ILogger<DemoCatalogSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("DemoCatalog:Enabled")) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var teachers = await userManager.GetUsersInRoleAsync(Roles.Teacher);
        var teacher = teachers.OrderBy(user => user.Email, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (teacher is null)
        {
            logger.LogWarning("Demo catalogue is enabled but no teacher account exists. Assign the Teacher role and restart the API to seed it.");
            return;
        }

        var categories = await db.Categories.ToListAsync(cancellationToken);
        var categoryIds = categories.ToDictionary(category => category.Name, category => category.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var seed in CourseSeeds)
        {
            if (categoryIds.ContainsKey(seed.Category)) continue;
            var category = Category.Create(seed.Category, $"Starter courses for curious learners in {seed.Category}.");
            db.Categories.Add(category);
            categoryIds.Add(seed.Category, category.Id);
        }
        await db.SaveChangesAsync(cancellationToken);

        categories = await db.Categories.ToListAsync(cancellationToken);
        var existingCourses = await db.Courses
            .Where(course => course.ImageUrl == null && course.Status != CourseStatus.Archived)
            .ToListAsync(cancellationToken);
        foreach (var course in existingCourses)
        {
            var categoryName = categories.FirstOrDefault(category => category.Id == course.CategoryId)?.Name;
            course.SetImageUrl(CoverFor(categoryName));
        }
        await db.SaveChangesAsync(cancellationToken);

        var newCourses = 0;
        var newLessons = 0;
        var newAssignments = 0;
        foreach (var seed in CourseSeeds)
        {
            var course = await db.Courses.SingleOrDefaultAsync(
                item => item.InstructorUserId == teacher.Id && item.Title == seed.Title,
                cancellationToken);
            if (course is null)
            {
                course = Course.Create(
                    teacher.Id, categoryIds[seed.Category], seed.Title, seed.Summary, seed.Description, seed.ImageUrl);
                course.SubmitForReview();
                course.Approve();
                db.Courses.Add(course);
                newCourses++;
                await db.SaveChangesAsync(cancellationToken);
            }

            var lessonPositions = await db.Lessons
                .Where(lesson => lesson.CourseId == course.Id)
                .Select(lesson => lesson.Position)
                .ToListAsync(cancellationToken);
            for (var index = 0; index < seed.Lessons.Length; index++)
            {
                var position = index + 1;
                if (lessonPositions.Contains(position)) continue;
                var lesson = seed.Lessons[index];
                db.Lessons.Add(Lesson.Create(course.Id, lesson.Title, lesson.Content, position));
                newLessons++;
            }

            var assignmentTitles = await db.Assignments
                .Where(assignment => assignment.CourseId == course.Id)
                .Select(assignment => assignment.Title)
                .ToListAsync(cancellationToken);
            foreach (var assignment in seed.Assignments)
            {
                if (assignmentTitles.Contains(assignment.Title, StringComparer.OrdinalIgnoreCase)) continue;
                db.Assignments.Add(CourseAssignment.Create(
                    course.Id, assignment.Title, assignment.Instructions, null, 2, 100m));
                newAssignments++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Demo catalogue ready. Added {CourseCount} courses, {LessonCount} lessons, and {AssignmentCount} assignments; assigned cover photos to {ExistingCourseCount} existing courses.",
            newCourses, newLessons, newAssignments, existingCourses.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed record LessonSeed(string Title, string Content);
    private sealed record AssignmentSeed(string Title, string Instructions);
    private sealed record CourseSeed(
        string Category,
        string Title,
        string Summary,
        string Description,
        string ImageUrl,
        LessonSeed[] Lessons,
        AssignmentSeed[] Assignments);

    private static readonly CourseSeed[] CourseSeeds =
    [
        new("Mathematics", "Patterns, Puzzles & Problem Solving",
            "Find the hidden rules behind number patterns, puzzles, and everyday maths.",
            "Build confidence by looking for structure, testing ideas, and explaining why an answer works. This course uses short challenges to make mathematical thinking feel practical and rewarding.",
            "/images/courses/mathematics.jpg",
            [
                new("Notice what changes", "A pattern is a repeatable change. Look at 4, 7, 10, 13. Each term grows by 3. Before guessing the next number, describe the change in words and check it against every pair."),
                new("Even, odd, and multiples", "An even number can be grouped into pairs with none left over. Multiples of 4 are 4, 8, 12, 16, and so on. Try skip-counting, drawing groups, and using the last digit to spot helpful clues."),
                new("Turn a pattern into a rule", "A rule tells you how to make every term. For 5, 8, 11, 14, start at 5 and add 3 each time. Write the first few terms from your rule to see whether it matches the pattern."),
                new("Choose a puzzle strategy", "When a problem feels large, make a table, draw a picture, or work backwards from the result. Keep track of what you have tried so that you do not repeat the same step."),
                new("Explain how you know", "A strong maths explanation shows the idea, the calculation, and a check. If another learner can follow your steps and reach the same answer, your reasoning is clear."),
            ],
            [
                new("Invent a number pattern", "Create a pattern with at least 8 terms. Write its rule, circle the 5th and 8th terms, and explain how you checked that the rule works. Include one question for a classmate."),
                new("Plan a class celebration", "A class has 24 pupils. Compare snack packs costing 3, 4, or 6 credits each. Show which pack sizes split evenly, choose a budget-friendly option, and explain your calculations."),
            ]),
        new("Science", "Little Lab: Matter, Mixtures & Motion",
            "Explore materials, fair experiments, and the forces that make things move.",
            "Practice the habits of a scientist: observe carefully, measure fairly, and use evidence to explain what happened. Every activity can be explored with safe household materials and adult guidance where needed.",
            "/images/courses/science-lab.jpg",
            [
                new("Ask a testable question", "A useful science question can be investigated by collecting evidence. Instead of asking whether a ramp is fun, ask how changing its height affects how far a toy car travels."),
                new("Measure materials", "Matter has mass and takes up space. Use the same container and units when comparing samples. Write down what you observe before deciding what it means."),
                new("Separate a mixture", "A mixture combines materials that keep their own properties. Sieving separates large and small pieces; filtering catches solids in a liquid. Choose a method based on the materials you can see."),
                new("Explore pushes and pulls", "A force is a push or a pull. It can start, stop, speed up, slow down, or change the direction of an object. Friction is a force that resists sliding between surfaces."),
                new("Make a fair test", "Change one factor at a time and keep the others the same. Repeat a measurement when you can. Fair tests make it easier to decide whether the changed factor caused the result."),
            ],
            [
                new("Design a fair ramp test", "Investigate how ramp height changes a toy car's travel distance. State your question, identify the one factor you change, list what stays the same, record three trials, and write a conclusion from your results."),
                new("Choose a separation method", "Plan how you would separate a mixture of rice, sand, and water using safe household tools. Draw or describe each step and explain which material each step removes."),
            ]),
        new("Coding", "Creative Coding: Your First Web Games",
            "Learn to think like a programmer while planning a tiny interactive game.",
            "Start with clear instructions, then use events, repetition, and simple variables to describe how a game behaves. No coding background is needed; each lesson connects a programming idea to something you can see and test.",
            "/images/courses/coding.jpg",
            [
                new("Give precise instructions", "An algorithm is a set of steps for solving a problem. Write directions for a partner to move through a small grid. If a step is unclear, improve it and try again."),
                new("Put steps in sequence", "Programs run instructions in an order. A game might first show a title, then wait for a key press, then move a character. Rearranging steps can change the result."),
                new("React to events", "An event is something a program notices, such as a button press or a character touching a wall. Event rules connect a player's action to what happens next."),
                new("Repeat with loops", "A loop repeats instructions. Instead of writing the same step many times, describe how often it repeats or what condition makes it stop. Loops make patterns and animations easier to build."),
                new("Use variables for score", "A variable stores a value that can change, like a score or number of lives. Decide when the value increases, decreases, or resets, then test the rule with a few examples."),
            ],
            [
                new("Plan a catch-the-star game", "Create a one-page game plan. Draw the player and target, write the start instructions, describe two keyboard events, and explain how the score changes. Include one rule that prevents endless play."),
                new("Debug a treasure hunt", "A robot must move 3 steps forward, turn right, and move 2 more steps. Draw the route on a grid, find one ambiguous instruction, rewrite it precisely, and explain how a loop could simplify the route."),
            ]),
        new("English", "Story Builders: Reading & Writing with Confidence",
            "Read for clues, grow your vocabulary, and shape a story with a satisfying ending.",
            "Use evidence from a text to understand characters and events, then apply those ideas to your own writing. The activities work with a book or short story you already enjoy.",
            "/images/courses/reading.jpg",
            [
                new("Read for the main idea", "The main idea is what a section is mostly about. Notice repeated details, headings, and what changes from the beginning to the end. Summarize the section in one sentence."),
                new("Collect clues about characters", "A reader learns about a character from actions, words, and choices. Find a moment that shows a quality, then explain what the moment suggests instead of only naming the quality."),
                new("Use context for new words", "When you meet an unfamiliar word, read the sentence around it and look for examples, contrasts, or explanations. Make a sensible guess, then check a dictionary if one is available."),
                new("Build a clear story shape", "Stories often introduce a character and goal, create a problem, and show how the character responds. A simple plan helps each scene move the story forward."),
                new("Revise for your reader", "Revision means making an idea clearer, not just fixing spelling. Read your draft aloud, replace confusing words, and check that each paragraph helps the reader follow the story."),
            ],
            [
                new("Write a small turning-point story", "Write a story of 3 to 5 paragraphs about a character who solves an unexpected problem. Include a clear goal, two clues, and an ending that follows from the character's choices."),
                new("Recommend a story", "Choose a story or book you have read. Write a short recommendation with one spoiler-free summary, one specific detail you liked, and the kind of reader who might enjoy it."),
            ]),
        new("Art", "Color, Texture & Everyday Art",
            "Try color, shape, and texture while turning ordinary observations into visual ideas.",
            "Learn a few practical ways to plan an image, choose materials, and explain a creative decision. You can work with pencils, paper, paint, or a digital drawing tool.",
            "/images/courses/art.jpg",
            [
                new("Look closely before you draw", "Artists notice shape, light, and small details. Choose a simple object and sketch its largest shapes first. Looking back and forth between the object and the page helps your drawing stay connected to what you see."),
                new("Mix a useful color palette", "Primary colors can be combined to make many other colors. A palette is a small group of colors chosen to work together. Test a few mixes on scrap paper before using them in your main piece."),
                new("Use contrast to guide attention", "Contrast is a visible difference, such as light beside dark or a large shape beside a small one. Use contrast to help viewers notice the most important part of your picture first."),
                new("Explore texture", "Texture describes how a surface looks or feels. Dots, short lines, layered paper, and rubbing over a textured surface can all suggest different materials. Choose a technique that fits the object."),
                new("Explain an art choice", "An artist statement gives a viewer a useful way into the work. Name one choice you made, such as color or composition, and explain what you wanted that choice to communicate."),
            ],
            [
                new("Design a curiosity poster", "Create a poster that invites someone to explore a subject you enjoy. Use a clear focal point, no more than four main colors, and a short title. Describe one design decision and why it helps the message."),
                new("Make a texture collection", "Collect or draw four different textures from safe everyday objects. Label how you represented each texture and write a few sentences about which technique was easiest to recognize."),
            ]),
        new("Geography", "Map Makers: Our Amazing Planet",
            "Read maps, compare places, and explain how land and climate shape daily life.",
            "Practice using map keys, direction, and scale while thinking about connections between people and the places they live. You will finish by creating a map that helps someone make a real decision.",
            "/images/courses/geography.jpg",
            [
                new("Read a map key", "A map key explains the symbols used on a map. Before following a route, check the key, direction marker, and scale. These clues help turn a picture into useful information."),
                new("Use direction and coordinates", "North, south, east, and west describe direction. Grid references divide a map into smaller spaces so you can describe where something is. Follow the map's own labels carefully."),
                new("Compare landforms", "Mountains, valleys, plains, rivers, and coasts create different opportunities and challenges. Use what you can observe on a map to explain how a landform might affect travel or settlement."),
                new("Weather and climate", "Weather describes conditions over a short time; climate describes patterns over many years. A place's climate influences the plants, clothing, and activities people may choose."),
                new("Plan a helpful route", "A good route considers distance, safety, and the places someone needs to reach. Use a map key and scale to compare options, then explain why your route suits the traveller."),
            ],
            [
                new("Map a safe walk", "Draw a simple map from a familiar starting point to a destination. Add a title, north arrow, key, three landmarks, and a route. Explain one safety choice you made."),
                new("Compare two places", "Choose two places with different landforms or climates. List two differences you can support with map or weather information, then describe one way people might adapt to each place."),
            ]),
        new("Science", "Space Explorers: Moon, Stars & Planets",
            "Use observation and models to explore the Moon, planets, and our place in space.",
            "Build a scale-aware picture of the solar system and explain patterns in the sky. The activities use simple models and observations rather than requiring special equipment.",
            "/images/courses/space.jpg",
            [
                new("Meet the solar system", "The Sun is a star at the centre of our solar system. Planets travel around it, but they are very different in size and distance. A model can show order, though not every distance at once."),
                new("Why the Moon changes shape", "The Moon does not make its own light; sunlight illuminates the side facing the Sun. As the Moon moves around Earth, we see different portions of its lit half. This creates the familiar phases."),
                new("Day, night, and orbit", "Earth rotates once about every day, bringing different places into sunlight. Earth also travels around the Sun. A lamp and ball can model these motions if you keep track of the light source."),
                new("Notice patterns in the sky", "Careful observers record the date, time, and direction of what they see. Repeating an observation helps reveal patterns. Never look directly at the Sun; use safe, adult-approved materials for solar observations."),
                new("Compare planets carefully", "A useful comparison uses the same facts for each planet, such as size, distance, or surface conditions. Avoid drawing a model that makes every planet appear equally far apart if distance is the idea you are explaining."),
            ],
            [
                new("Build a scale-aware solar system", "Make a model or labelled diagram showing the Sun and eight planets in order. Include one comparison of size or distance, and write a note explaining what your model cannot show accurately."),
                new("Keep a Moon observation journal", "Over several days, sketch the Moon if it is visible. Record the date, time, and approximate shape. If weather prevents an observation, mark it as missed instead of guessing."),
            ]),
        new("Study Skills", "Study Smarter: Notes, Focus & Memory",
            "Make a realistic study routine using short focus sessions and memory-friendly practice.",
            "Learn how to set a small goal, organize useful notes, and check what you remember without relying only on rereading. These habits can support any school subject.",
            "/images/courses/learning-habits.jpg",
            [
                new("Choose one clear goal", "A useful goal describes an action you can finish, such as solving five fraction problems or summarizing one page. Small goals make it easier to begin and notice your progress."),
                new("Try active recall", "After learning an idea, close your notes and try to explain it from memory. Then check what you missed. This shows which parts need another look more clearly than rereading alone."),
                new("Space out your practice", "A short review today and another review later can help you remember more than one long session. Mix earlier ideas with new ones so you practice choosing the right method."),
                new("Make notes you can use", "Good notes capture key ideas in your own words. Use headings, short examples, and questions. Leave space to add a correction or a new connection later."),
                new("Protect your focus", "Choose a small task, gather what you need, and reduce one distraction before you begin. Take a short break after a focused session, then decide what the next small step should be."),
            ],
            [
                new("Create a five-day study plan", "Choose one school topic and plan five short study sessions. For each day, write a specific task and a quick way to check learning. Include at least one spaced review and one rest day."),
                new("Teach a concept back", "Pick a concept from any subject. Explain it in simple language as if teaching a younger pupil, then list one question you still have and how you could find a reliable answer."),
            ]),
        new("Science", "Everyday Physics: Motion & Simple Machines",
            "Discover how forces, ramps, levers, and wheels make everyday tasks easier.",
            "Connect physics ideas to familiar objects through safe observations and clear diagrams. Learn to describe how a force changes motion and how a simple machine can redirect that force.",
            "/images/courses/physics.jpg",
            [
                new("Describe motion", "Motion is a change in position over time. A reference point helps you decide whether something has moved. Compare two objects over the same time to begin describing which travels farther."),
                new("Balance forces", "A balanced force does not change an object's motion. An unbalanced force can make it speed up, slow down, or turn. Draw arrows to show which way a force acts."),
                new("Explore ramps", "A ramp is an inclined plane. It can reduce the force needed to raise an object by spreading the lift over a longer distance. The surface and slope both affect movement."),
                new("Try a lever", "A lever turns around a fixed point called a fulcrum. Moving the fulcrum changes how much effort is needed and how far the other end travels. Use only safe, lightweight objects for a model."),
                new("Use evidence to explain", "A physics explanation connects an observation to a force or machine. State what changed, describe the evidence, and link the evidence to the idea you are using."),
            ],
            [
                new("Build a paper lever model", "Draw a lever with a fulcrum and two loads. Show where you would apply effort, predict what happens when the fulcrum moves, and explain why a longer effort arm can help."),
                new("Compare two ramp surfaces", "Plan a safe test comparing how a toy car moves on two ramp surfaces. Keep the ramp height and car the same, record three trials for each surface, and explain a pattern you observe."),
            ]),
    ];

    private static string CoverFor(string? categoryName)
    {
        var name = categoryName?.ToLowerInvariant() ?? string.Empty;
        if (name.Contains("math", StringComparison.Ordinal)) return "/images/courses/mathematics.jpg";
        if (name.Contains("physics", StringComparison.Ordinal)) return "/images/courses/physics.jpg";
        if (name.Contains("science", StringComparison.Ordinal) || name.Contains("biology", StringComparison.Ordinal))
            return "/images/courses/science-lab.jpg";
        if (name.Contains("space", StringComparison.Ordinal) || name.Contains("astronomy", StringComparison.Ordinal))
            return "/images/courses/space.jpg";
        if (name.Contains("code", StringComparison.Ordinal) || name.Contains("program", StringComparison.Ordinal) ||
            name.Contains("technology", StringComparison.Ordinal) || name.Contains("data", StringComparison.Ordinal) ||
            name.Contains("software", StringComparison.Ordinal)) return "/images/courses/coding.jpg";
        if (name.Contains("english", StringComparison.Ordinal) || name.Contains("language", StringComparison.Ordinal) ||
            name.Contains("reading", StringComparison.Ordinal)) return "/images/courses/reading.jpg";
        if (name.Contains("art", StringComparison.Ordinal) || name.Contains("design", StringComparison.Ordinal))
            return "/images/courses/art.jpg";
        if (name.Contains("geography", StringComparison.Ordinal) || name.Contains("earth", StringComparison.Ordinal))
            return "/images/courses/geography.jpg";
        if (name.Contains("study", StringComparison.Ordinal) || name.Contains("life", StringComparison.Ordinal))
            return "/images/courses/learning-habits.jpg";
        return "/images/courses/learning-habits.jpg";
    }
}
