using FluentValidation;
using test_project.Models.DTO;

namespace test_project.Validators;

public class CreateSpreadDtoValidator : AbstractValidator<CreateSpreadDto>
{
    public CreateSpreadDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название расклада обязательно")
            .MaximumLength(200).WithMessage("Название не должно превышать 200 символов");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Описание расклада обязательно")
            .MaximumLength(2000).WithMessage("Описание не должно превышать 2000 символов");

        RuleFor(x => x.NumberOfPositions)
            .GreaterThan(0).WithMessage("Количество позиций должно быть больше 0")
            .LessThanOrEqualTo(50).WithMessage("Количество позиций не должно превышать 50");
    }
}
