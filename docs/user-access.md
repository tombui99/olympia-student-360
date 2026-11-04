# Teacher access

Both Account Management and User Access Management support assignments by class, grade or school level.
Select **Grade / Khối** and one or more grades (1–12) to include students in every class of those grades.
The server uses each student's SIS grade, falling back to Canvas grade only when the SIS grade is empty.
It does not infer grade from a class name.

School levels follow Olympia's operating model:

| School level | Grades |
| --- | --- |
| Primary / Tiểu học | 1–4 |
| Lower Secondary / THCS | 5–8 |
| Upper Secondary / THPT | 9–12 |

Class, grade and school-level assignments add together. For example, Grade 8 plus class 9A includes
all Grade 8 students and students in 9A. **All** overrides these restrictions. Grade access grants
academic access, like class and school-level access; health and bus modules require their own scopes.
When a population restriction accompanies health or bus access, it also limits those modules' students.
Unlinked records require an explicit matching class assignment; a grade cannot be guessed from their labels.

Assignments are stored in `TeacherGradeAccesses` with a unique user/grade pair and a cascading user foreign key.
API requests validate grades and scopes before replacing assignments. Permissions are reloaded on each
authenticated API request, and grade assignments participate in report cache keys. Reload the page to
refresh the displayed navigation after an administrator changes access.

Migration `20261104161615_AddTeacherGradeAccessAndCorrectSchoolLevels` creates the grade-assignment table,
corrects stored Grade 9 education levels to Upper Secondary, and queues affected imported-report summaries
for refresh. Canvas scores, campus and programme are retained. No full SIS reimport is required.
Existing Lower Secondary assignments now cover 5–8; existing Upper Secondary assignments now cover 9–12.
