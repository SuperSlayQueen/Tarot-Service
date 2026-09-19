using FluentValidation;
using test_project.Models.DTO;

namespace test_project.Validators;

public class CreateDeckDtoValidator : AbstractValidator<CreateDeckDto>
{
    public CreateDeckDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название колоды обязательно")
            .MaximumLength(200).WithMessage("Название не должно превышать 200 символов");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Описание не должно превышать 2000 символов");

        RuleFor(x => x.Author)
            .MaximumLength(200).WithMessage("Автор не должен превышать 200 символов");
    }
}
